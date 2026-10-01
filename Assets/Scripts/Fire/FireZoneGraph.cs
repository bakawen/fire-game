using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 活火场（办公楼）：区域火/烟蔓延图——决策的后果由火场自己涌现。
/// 火与烟沿区域连接实时扩散：门=阀门（关着的门挡烟挡火，玩家自己推开的门放火进来）、
/// 防火卷帘=联动阀门（报警后落下即封链，边门可重开）、喷淋=按区热激活的火势压制、
/// 断电=全局减速+电气起源衰减、灭火器只对"初期窗口"内的起源火有效（窗口一关即不可灭）。
/// 逐区驱动 SmokeVolume（可见浓烟+玩家伤害）与 FirePoint（按区火势阈值点燃/按余烬线熄灭），
/// 取代 FireSpreadManager 在本关的定时表职责（宿舍/公寓继续用旧管理器）。
/// 起火点从 origins 随机选择（forcedOrigin≥0 强制，测试用），延迟到首 tick 才选——确保出生点已就位。
/// 90秒火场节奏（2026-09-28 重构）：临界/封区阈值序列化，供死线与广播系统接线。
/// </summary>
public class FireZoneGraph : MonoBehaviour
{
    [System.Serializable]
    public class Zone
    {
        public string id;
        public SmokeVolume volume;               // 空=纯逻辑区（无显示无伤害）
        public Vector3 center = Vector3.zero;     // 逻辑区盒中心（接线时与体积对齐）
        public Vector3 size = Vector3.one * 6f;  // 逻辑区盒尺寸
        public FirePoint[] fires;                 // 区内火点（含休眠的起源候选火）
        public float[] igniteThresholds;          // 区火势跨过阈值→点燃；低于阈值-0.18→外部衰减熄灭
        public float growthPerSecond = 0.010f;    // 已有火区的自然增长（比例式）
        [NonSerialized] public float fire;
        [NonSerialized] public float smoke;
        [NonSerialized] public float smokeBoost;      // 外部烟度增益（照明故障区等事件）
        [NonSerialized] public bool sprinklerActive;  // 本区喷淋已启动（火增长×压制系数）
    }

    [System.Serializable]
    public class Link
    {
        public int a, b;
        public InteractableDoor door;             // 空=常开连接
        public FireShutter shutter;               // 空=无卷帘（报警联动阀门）
        public float smokePermeability = 0.30f;   // 烟扩散速率
        public float smokeClosedFactor = 0.006f;  // 关门后剩余渗透比例（时间常数>9分钟，长局可挡烟）
        public float firePermeabilityFactor = 0.45f; // 火相对烟的扩散速率
        [NonSerialized] public bool open = true;
    }

    [System.Serializable]
    public class OriginConfig
    {
        public string label;
        public FirePoint fire;
        public int zoneIndex;
        public float startFireLevel = 0.28f;
        public float originGrowth = 0.005f;      // 源头下限曲线斜率（每秒）
        public float reigniteFireLevel = 0.30f;  // 复燃时的区火势下限
        public bool electrical = true;
    }

    [Header("区域与连接")]
    [SerializeField] private List<Zone> zones = new List<Zone>();
    [SerializeField] private List<Link> links = new List<Link>();

    [Header("随机起火点")]
    [SerializeField] private List<OriginConfig> origins = new List<OriginConfig>();
    [Tooltip("≥0 强制该下标起火点（测试用）；-1=随机（避开玩家出生点 4m 内）")]
    [SerializeField] private int forcedOrigin = -1;

    [Header("规则参数")]
    [SerializeField] private float tickSeconds = 0.5f;
    [SerializeField, Range(0.2f, 1f)] private float powerCutGrowthFactor = 0.72f;
    [SerializeField] private float powerCutOriginDecay = 0.035f;   // 断电后电气起源区每秒衰减
    [SerializeField] private float suppressionSeconds = 18f;      // 未断电时起源火被扑灭后的复燃倒计时
    [SerializeField] private float extinguishableWindow = 0.55f;   // 起源区火势低于此=起源火可灭
    [SerializeField] private float smokeFromFire = 0.6f;          // 区内火→烟下限
    [SerializeField] private float maxFogDensity = 0.10f;
    [Tooltip("非起源区火势低于此值视为余烬，按比例消退")]
    [SerializeField] private float emberFadeBelow = 0.06f;
    [Tooltip("喷淋启动后该区火增长乘数（控火不是灭火）")]
    [SerializeField, Range(0.1f, 0.95f)] private float sprinklerSuppression = 0.55f;

    [Header("90秒火场：死线与广播阈值")]
    [Tooltip("区火势越过此值=轰燃临界（OfficeFlashover 警告 + StructuralFailure 死线）")]
    [SerializeField] private float zoneCriticalLevel = 0.80f;
    [Tooltip("区烟度越过此值=浓烟封区（烟感点名广播）")]
    [SerializeField] private float zoneSmokyLevel = 0.35f;

    [Header("调试（交付前关闭）")]
    [SerializeField] private bool debugOverlay = false;

    public float Elapsed { get; private set; }
    public int ActiveOriginIndex { get; private set; } = -1;
    public string ActiveOriginLabel => origin != null ? origin.label : "";
    public bool PowerCutApplied => powerCut;
    public bool OriginExtinguished => originExtinguished;
    public int ZoneCount => zones != null ? zones.Count : 0;

    /// <summary>全楼总火势（0~zone数；死线时长折算用）。</summary>
    public float TotalFire
    {
        get
        {
            if (zones == null) return 0f;
            float total = 0f;
            for (int i = 0; i < zones.Count; i++)
                if (zones[i] != null) total += zones[i].fire;
            return total;
        }
    }

    /// <summary>任一区火势首次越过临界（轰燃前兆，OfficeFlashover/StructuralFailure 订阅）。参数=区下标。</summary>
    public event Action<int> OnZoneCritical;
    /// <summary>任一区烟度首次越过阈值（浓烟封区，烟感点名广播订阅）。参数=区下标。</summary>
    public event Action<int> OnZoneSmoky;
    /// <summary>起火点选定后触发（记账用）。</summary>
    public event Action OnOriginSelected;
    /// <summary>起源火被灭火器扑灭（未断电=临时压制，会复燃）。</summary>
    public event Action OnOriginSuppressed;
    /// <summary>断电后电气起源火永久根除（不再复燃）。</summary>
    public event Action OnOriginRooted;
    /// <summary>未断电的电气起源火复燃。</summary>
    public event Action OnOriginReignited;

    private OriginConfig origin;
    private Zone originZone;
    private bool powerCut;
    private bool originExtinguished;
    private bool originPicked;
    private bool rootedFired;
    private float reigniteAt = -1f;
    private float difficultyScale = 1f;
    private float lastTick = -999f;
    private readonly HashSet<int> criticalFired = new HashSet<int>();
    private readonly HashSet<int> smokyAnnounced = new HashSet<int>();

    private void Start()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.44f, 0.42f, 0.39f);
        RenderSettings.fogDensity = 0f;
    }

    private void OnEnable()
    {
        // 门=阀门：订阅所有带门的连接（开门=高速通道，关门=近零渗透）
        foreach (var l in links)
        {
            if (l == null || l.door == null) continue;
            l.open = l.door.IsOpen;
            var link = l;
            link.door.OnOpened += d => SetLinkOpen(link, true);
            link.door.OnClosed += d => SetLinkOpen(link, false);
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0.01f) return;
        Elapsed += Time.deltaTime;

        if (!originPicked && Elapsed >= tickSeconds) PickOrigin();
        if (Elapsed - lastTick >= tickSeconds)
        {
            Tick(Mathf.Min(Elapsed - lastTick, tickSeconds * 1.5f));
            lastTick = Elapsed;
        }
    }

    // ————— 外部接口 —————

    /// <summary>断电：全局增长减速；电气起源火开始衰减且不再复燃。</summary>
    public void SetPowerCut(bool on)
    {
        if (powerCut == on) return;
        powerCut = on;
        if (on)
        {
            reigniteAt = -1f;
            // 先压制后断电：复燃取消 = 火被根除
            if (originExtinguished && !rootedFired) { rootedFired = true; OnOriginRooted?.Invoke(); }
            if (origin != null && origin.electrical && origin.fire != null && origin.fire.IsBurning)
                origin.fire.StartExternalDecay(0.12f);   // 必须压过火点自身增长(0.05)，否则永不死透
        }
    }

    /// <summary>难度缩放（随机出生点系统调用，作用于起源区增长）。</summary>
    public void SetDifficulty(float scale) => difficultyScale = Mathf.Clamp(scale, 0.3f, 3f);

    public float GetSmokeAt(Vector3 pos)
    {
        var z = ZoneAt(pos);
        return z != null ? z.smoke : 0f;
    }

    public string GetZoneIdAt(Vector3 pos)
    {
        var z = ZoneAt(pos);
        return z != null ? z.id : "";
    }

    public string GetZoneId(int index)
    {
        return (zones != null && index >= 0 && index < zones.Count && zones[index] != null) ? zones[index].id : ("Zone" + index);
    }

    /// <summary>区火势（供断言/复盘查询）。</summary>
    public float GetZoneFire(int index)
    {
        return (zones != null && index >= 0 && index < zones.Count && zones[index] != null) ? zones[index].fire : -1f;
    }

    public float GetZoneSmoke(int index)
    {
        return (zones != null && index >= 0 && index < zones.Count && zones[index] != null) ? zones[index].smoke : -1f;
    }

    /// <summary>位置所在区下标（不在任何区盒返回 -1）——环境语言系统按区归类探测器用。</summary>
    public int GetZoneIndexAt(Vector3 pos)
    {
        if (zones == null) return -1;
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (z == null) continue;
            Vector3 h = z.size * 0.5f;
            if (Mathf.Abs(pos.x - z.center.x) <= h.x
                && Mathf.Abs(pos.y - z.center.y) <= h.y
                && Mathf.Abs(pos.z - z.center.z) <= h.z)
                return i;
        }
        return -1;
    }

    /// <summary>接线：给某条链挂防火卷帘（报警联动阀门）。</summary>
    public void AssignShutter(int linkIndex, FireShutter shutter)
    {
        if (links == null || linkIndex < 0 || linkIndex >= links.Count || links[linkIndex] == null) return;
        links[linkIndex].shutter = shutter;
    }

    /// <summary>查询某条链当前是否关闭（门关或卷帘落）。测试/断言用。</summary>
    public bool IsLinkClosed(int linkIndex)
    {
        if (links == null || linkIndex < 0 || linkIndex >= links.Count || links[linkIndex] == null) return false;
        var l = links[linkIndex];
        return (l.door != null && !l.open) || (l.shutter != null && l.shutter.IsDown);
    }

    /// <summary>喷淋系统按区启停（火增长×sprinklerSuppression）。</summary>
    public void SetZoneSprinkler(int index, bool on)
    {
        if (zones == null || index < 0 || index >= zones.Count || zones[index] == null) return;
        zones[index].sprinklerActive = on;
    }

    public bool IsZoneSprinklerOn(int index)
    {
        return zones != null && index >= 0 && index < zones.Count && zones[index] != null && zones[index].sprinklerActive;
    }

    /// <summary>外部烟度增益（照明故障区等）：叠加到该区烟下限。</summary>
    public void SetZoneSmokeBoost(int index, float boost)
    {
        if (zones == null || index < 0 || index >= zones.Count || zones[index] == null) return;
        zones[index].smokeBoost = Mathf.Max(0f, boost);
    }

    /// <summary>测试接口：直接设定区火势（不触发点燃流程；下一 tick 自然越过临界/封区阈值）。</summary>
    public void SetZoneFireForTest(int index, float value)
    {
        if (zones == null || index < 0 || index >= zones.Count || zones[index] == null) return;
        zones[index].fire = Mathf.Clamp01(value);
    }

    // ————— 模拟 —————

    private void PickOrigin()
    {
        originPicked = true;
        if (origins == null || origins.Count == 0) return;

        int idx = (forcedOrigin >= 0 && forcedOrigin < origins.Count) ? forcedOrigin : -1;
        if (idx < 0 && RetryRun.Active && RetryRun.OriginIndex >= 0 && RetryRun.OriginIndex < origins.Count)
        {
            // 同局重试：死亡后"重新开始"——同一随机起火点，带着教训重做同一道火场题
            idx = RetryRun.OriginIndex;
        }
        if (idx < 0)
        {
            var player = GameObject.Find("Player");
            Vector3 pp = player != null ? player.transform.position : Vector3.zero;
            idx = 0;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int roll = UnityEngine.Random.Range(0, origins.Count);
                var o = origins[roll];
                if (o == null || o.fire == null) continue;
                if (Vector3.Distance(o.fire.transform.position, pp) < 4f) continue; // 别把火点在玩家脸上
                idx = roll;
                break;
            }
        }
        RetryRun.OriginIndex = idx;   // 记住本题，供同局重试

        origin = origins[idx];
        ActiveOriginIndex = idx;
        originZone = (origin != null && origin.zoneIndex >= 0 && origin.zoneIndex < zones.Count) ? zones[origin.zoneIndex] : null;
        if (origin == null || origin.fire == null) { origin = null; return; }

        origin.fire.Ignite();
        origin.fire.SetGrowth(0.05f);
        if (originZone != null) originZone.fire = Mathf.Max(originZone.fire, origin.startFireLevel);
        origin.fire.OnExtinguished += OnOriginFireExtinguished;
        OnOriginSelected?.Invoke();
    }

    private void OnOriginFireExtinguished(FirePoint fp)
    {
        if (origin == null || fp != origin.fire) return;
        if (!originExtinguished)
        {
            originExtinguished = true;
            if (originZone != null) originZone.fire = Mathf.Min(originZone.fire, 0.15f);
            if (powerCut)
            {
                if (!rootedFired) { rootedFired = true; OnOriginRooted?.Invoke(); }
            }
            else
            {
                reigniteAt = Elapsed + suppressionSeconds;
                OnOriginSuppressed?.Invoke();
            }
        }
        else if (powerCut && !rootedFired)
        {
            // 断电后才真正死透（外部衰减到位）
            rootedFired = true;
            OnOriginRooted?.Invoke();
        }
    }

    private void SetLinkOpen(Link l, bool open) => l.open = open;

    private void Tick(float dt)
    {
        if (zones == null || zones.Count == 0) return;

        float growFactor = powerCut ? powerCutGrowthFactor : 1f;

        // 1) 增长/衰减（未处置的起源区火势由"源头下限"在扩散后统一托底）
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (z == null) continue;
            if (z == originZone)
            {
                if (originExtinguished)
                {
                    // 已被压制：未断电维持低位闷烧；断电则持续衰减
                    z.fire = Mathf.Max(0f, z.fire - (powerCut ? powerCutOriginDecay : 0.012f) * dt);
                }
                else if (powerCut && origin != null && origin.electrical)
                {
                    z.fire = Mathf.Max(0f, z.fire - powerCutOriginDecay * dt);
                }
            }
            else if (z.fire > emberFadeBelow)
            {
                // 喷淋控火：该区自然增长×压制系数（争取时间，不是灭火）
                float zf = z.sprinklerActive ? sprinklerSuppression : 1f;
                z.fire = Mathf.Min(1f, z.fire * (1f + z.growthPerSecond * growFactor * zf * dt));
            }
            else if (z.fire > 0f)
            {
                z.fire = Mathf.Max(0f, z.fire * (1f - 0.06f * dt));   // 余烬按比例消退（线性会把摊薄的火抽干）
            }
        }

        // 2) 连接扩散（烟快火慢；关着的门/落下的卷帘几乎不通）
        for (int i = 0; i < links.Count; i++)
        {
            var l = links[i];
            if (l == null) continue;
            if (l.a < 0 || l.b < 0 || l.a >= zones.Count || l.b >= zones.Count) continue;
            var za = zones[l.a];
            var zb = zones[l.b];
            if (za == null || zb == null) continue;
            bool sealedByDoor = l.door != null && !l.open;
            bool sealedByShutter = l.shutter != null && l.shutter.IsDown;
            float doorFactor = (sealedByDoor || sealedByShutter) ? l.smokeClosedFactor : 1f;

            float ds = (za.smoke - zb.smoke) * l.smokePermeability * doorFactor * dt;
            za.smoke -= ds; zb.smoke += ds;

            float df = (za.fire - zb.fire) * l.smokePermeability * l.firePermeabilityFactor * doorFactor * dt;
            za.fire -= df; zb.fire += df;
        }

        // 3) 源头下限：起火房间永远是最热点——火势沿自身增长曲线托底，不会被扩散摊薄。
        //    喷淋压制的是蔓延（step1/扩散），压制不住已烧透的源头——死线由源头驱动，不受喷淋推迟。
        if (origin != null && originZone != null && !originExtinguished && !(powerCut && origin.electrical))
        {
            float floor = origin.startFireLevel
                + origin.originGrowth * (powerCut ? powerCutGrowthFactor : 1f) * difficultyScale * Elapsed;
            if (originZone.fire < floor) originZone.fire = floor;
        }

        // 3b) 烟：本区火产烟为下限（+事件烟度增益）；超出部分（扩散进来的）缓慢消散
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (z == null) continue;
            z.fire = Mathf.Clamp01(z.fire);
            float smokeFloor = Mathf.Clamp01(z.fire * smokeFromFire + z.smokeBoost);
            float rate = z.smoke < smokeFloor ? 0.15f : 0.015f;
            z.smoke = Mathf.MoveTowards(Mathf.Clamp01(z.smoke), smokeFloor, rate * dt);
        }

        // 4) 显示与伤害
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (z != null && z.volume != null) z.volume.SetLevel(z.smoke);
        }

        // 5) 火点：按区火势阈值点燃 / 余烬线熄灭（起源火由起源逻辑管理，跳过）
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (z == null || z.fires == null) continue;
            for (int k = 0; k < z.fires.Length; k++)
            {
                var f = z.fires[k];
                if (f == null) continue;
                if (origin != null && f == origin.fire) continue;
                float th = (z.igniteThresholds != null && k < z.igniteThresholds.Length)
                    ? Mathf.Clamp01(z.igniteThresholds[k]) : 0.35f;
                if (!f.IsBurning && z.fire >= th) { f.Ignite(); f.SetGrowth(0.05f); }
                else if (f.IsBurning && z.fire < th - 0.18f) f.StartExternalDecay(0.10f);
            }
        }

        // 6) 起源火可灭窗口：窗口一关（区火势越线）立即变不可灭 → 灭火器"恋战"链自动接管
        if (origin != null && origin.fire != null && originZone != null && !originExtinguished)
        {
            bool windowOpen = originZone.fire < extinguishableWindow;
            if (windowOpen != origin.fire.Extinguishable) origin.fire.SetExtinguishable(windowOpen);
        }

        // 7) 复燃倒计时：不断电的电气火被扑灭只是在争取时间
        if (originExtinguished && !powerCut && !rootedFired && reigniteAt > 0f && Elapsed >= reigniteAt
            && origin != null && origin.fire != null)
        {
            originExtinguished = false;
            reigniteAt = -1f;
            origin.fire.Ignite();
            if (originZone != null) originZone.fire = Mathf.Max(originZone.fire, origin.reigniteFireLevel);
            OnOriginReignited?.Invoke();
        }

        // 8) 轰燃临界（每区仅一次）：时机取决于玩家的处置——这是活火场的"软死线"
        for (int i = 0; i < zones.Count; i++)
        {
            if (criticalFired.Contains(i) || zones[i] == null) continue;
            if (zones[i].fire >= zoneCriticalLevel) { criticalFired.Add(i); OnZoneCritical?.Invoke(i); }
        }

        // 8b) 浓烟封区（每区仅一次）：烟感点名广播——楼在听觉上汇报失守
        for (int i = 0; i < zones.Count; i++)
        {
            if (smokyAnnounced.Contains(i) || zones[i] == null) continue;
            if (zones[i].smoke >= zoneSmokyLevel) { smokyAnnounced.Add(i); OnZoneSmoky?.Invoke(i); }
        }

        // 9) 全局雾=全楼总火势
        float total = 0f;
        for (int i = 0; i < zones.Count; i++)
            if (zones[i] != null) total += zones[i].fire;
        float target = Mathf.Clamp01(total / 4f) * maxFogDensity;
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, target, 1.2f * dt);
    }

    private Zone ZoneAt(Vector3 pos)
    {
        if (zones == null) return null;
        Zone best = null; float bestD = float.MaxValue;
        foreach (var z in zones)
        {
            if (z == null) continue;
            Vector3 h = z.size * 0.5f;
            bool inside = Mathf.Abs(pos.x - z.center.x) <= h.x
                       && Mathf.Abs(pos.y - z.center.y) <= h.y
                       && Mathf.Abs(pos.z - z.center.z) <= h.z;
            if (inside) return z;
            float d = Vector3.Distance(pos, z.center);
            if (d < bestD) { bestD = d; best = z; }
        }
        return bestD < 30f ? best : null;
    }

    // ————— 调试 —————

    private void OnGUI()
    {
        if (!debugOverlay) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"t={Elapsed:F0}s  origin[{ActiveOriginIndex}]={ActiveOriginLabel}  powerCut={powerCut}  suppressed={originExtinguished}  reigniteIn={(reigniteAt > 0f ? (reigniteAt - Elapsed).ToString("F0") : "-")}");
        if (zones != null)
            foreach (var z in zones)
                if (z != null) sb.AppendLine($"{z.id}: fire={z.fire:F2} smoke={z.smoke:F2}{(z.sprinklerActive ? " [喷淋]" : "")}{(z.smokeBoost > 0f ? $" +boost{z.smokeBoost:F2}" : "")}");
        GUI.Box(new Rect(12f, 12f, 380f, 40f + (zones != null ? zones.Count : 0) * 20f), sb.ToString());
    }
}
