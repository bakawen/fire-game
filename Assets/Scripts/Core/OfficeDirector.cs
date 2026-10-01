using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 办公楼导演系统（90秒火场）：三层随机总控——
/// ① 火场题面：随机起火点×随机出生点（FireZoneGraph/SpawnManager 自带，同局重试保留）；
/// ② 规则牌：每局抽 2/6（停电检修/水系统检修/卷帘检修/灭火器过期/加班高峰/独自加班）——改写本局可选动作与楼况；
/// ③ 事件牌：每局抽 2/5（呼救/假出口/背燃门/照明故障/手机诱饵）挂到随机候选槽——事件位置每局不同。
/// 同局重试（RetryRun）：LastSetup 静态保存整套抽签结果，死亡重开=同一道火场题。
/// 另负责：报警按钮/湿毛巾位置抽签、点火演出（灯闪爆响火球）、开局组合卡。
/// 全部槽位候选在场景预置（锚点/门/组件模板），本组件只做抽签+接线，不创建静态几何。
/// </summary>
public class OfficeDirector : MonoBehaviour
{
    public enum RuleCard
    {
        PowerMaintenance,      // 停电检修中：配电箱贴封条，本局无法断电
        SprinklerMaintenance,  // 水系统检修：喷淋永不启动
        ShutterMaintenance,    // 卷帘检修：报警不联动卷帘
        ExtinguisherExpired,   // 灭火器过期：剂量减半
        OvertimeCrowd,        // 加班高峰：呼救支线必出
        SoloNight              // 独自加班：呼救支线必不出
    }

    public enum EventCard { Distress, BlockedExit, Backdraft, LightingFault, PhoneLure }

    [Serializable]
    public class RunSetup
    {
        public RuleCard[] rules = Array.Empty<RuleCard>();
        public EventCard[] events = Array.Empty<EventCard>();
        public int alarmSlot;
        public int towelSlot;
        public int distressSlot = -1;
        public int blockedSlot = -1;
        public int backdraftSlot = -1;
        public int lightingSlot = -1;
        public int phoneSlot = -1;

        public bool HasRule(RuleCard r)
        {
            if (rules == null) return false;
            for (int i = 0; i < rules.Length; i++) if (rules[i] == r) return true;
            return false;
        }

        public bool HasEvent(EventCard e)
        {
            if (events == null) return false;
            for (int i = 0; i < events.Length; i++) if (events[i] == e) return true;
            return false;
        }
    }

    [Serializable]
    public class BlockedExitSlot
    {
        public InteractableDoor door;
        [Tooltip("假绿 EXIT 牌的世界位置（面向玩家来向）")]
        public Vector3 signPos;
        public Vector3 signEuler;
    }

    [Header("引用（留空自动查找）")]
    [SerializeField] private FireZoneGraph graph;
    [SerializeField] private AlarmButton alarmButton;
    [SerializeField] private WetTowel wetTowel;
    [SerializeField] private ElectricalPanel electricalPanel;
    [SerializeField] private FireExtinguisher fireExtinguisher;
    [SerializeField] private TutorialUI tutorial;
    [SerializeField] private RunIntroCard introCard;

    [Header("候选槽（场景预置）")]
    [Tooltip("报警按钮候选墙位（每局随机其一，按钮整体搬过去）")]
    [SerializeField] private Transform[] alarmCandidates;
    [Tooltip("湿毛巾候选位（每局随机其一）")]
    [SerializeField] private Transform[] towelCandidates;
    [SerializeField] private InteractableDoor[] distressDoorCandidates;
    [Tooltip("门后呼救模板（现有 DoorA(3) 下的 DistressZone——先停用，抽中再克隆到候选门）")]
    [SerializeField] private DistressCall distressTemplate;
    [SerializeField] private BlockedExitSlot[] blockedExitCandidates;
    [Tooltip("原烘焙的假绿 EXIT 牌（导演化后统一隐藏，抽中哪扇门就运行时生成哪块的牌）")]
    [SerializeField] private GameObject bakedFakeSign;
    [SerializeField] private InteractableDoor[] backdraftDoorCandidates;
    [SerializeField] private LightingFaultZone[] lightingFaultCandidates;
    [SerializeField] private PhoneLure[] phoneLureCandidates;

    [Header("规则牌视觉")]
    [Tooltip("配电箱检修封条（inactive，PowerMaintenance 时显示）")]
    [SerializeField] private GameObject panelSeal;
    [Tooltip("灭火器柜过期巡检签（inactive，ExtinguisherExpired 时显示）")]
    [SerializeField] private GameObject cabinetTag;

    [Header("点火演出")]
    [SerializeField] private Material fireMaterial;    // 火球材质（接 M_SoftFire）
    [SerializeField] private AudioClip boomClip;
    [SerializeField] private AudioClip backdraftBoomClip;   // 背燃门音爆（与点火爆区分）
    [SerializeField] private AudioSource sfx;

    [Header("测试（forced 掩码非 0 时替换抽签）")]
    [SerializeField] private int forcedRulesMask = 0;
    [SerializeField] private int forcedEventsMask = 0;
    [SerializeField] private int forcedAlarmSlot = -1;
    [SerializeField] private int forcedTowelSlot = -1;
    [SerializeField] private int forcedDistressSlot = -1;
    [SerializeField] private int forcedBlockedSlot = -1;
    [SerializeField] private int forcedBackdraftSlot = -1;
    [SerializeField] private int forcedLightingSlot = -1;
    [SerializeField] private int forcedPhoneSlot = -1;

    /// <summary>上一局的整套抽签（同局重试复现同一题面）。</summary>
    public static RunSetup LastSetup;

    /// <summary>本局抽签结果（测试断言用）。</summary>
    public RunSetup Current { get; private set; }

    private bool ignitionPlayed;

    private static readonly string[] RuleNames =
    {
        "配电检修·无法断电",
        "水系统检修·喷淋停用",
        "卷帘检修·联动停用",
        "灭火器过期·剂量减半",
        "加班高峰·楼里有人",
        "独自加班·楼里没人"
    };

    private void Awake()
    {
        // 拆除固定挂点必须赶在所有 Start 之前（Awake 期禁用的物体永不跑 Start——
        // 否则模板 DistressCall.Start() 会抢跑记账"听到呼救"）
        if (distressTemplate != null && distressTemplate.gameObject.activeSelf)
            distressTemplate.gameObject.SetActive(false);

        foreach (var be in UnityEngine.Object.FindObjectsByType<BlockedExitDoor>(FindObjectsSortMode.None))
        {
            if (be == null) continue;
            var d = be.GetComponent<InteractableDoor>();
            if (d != null) d.OpenInterceptor = null;
            Destroy(be);
        }
        if (bakedFakeSign != null && bakedFakeSign.activeSelf) bakedFakeSign.SetActive(false);
    }

    private void Start()
    {
        if (graph == null) graph = FindFirstObjectByType<FireZoneGraph>();
        if (alarmButton == null) alarmButton = FindFirstObjectByType<AlarmButton>();
        if (wetTowel == null) wetTowel = FindFirstObjectByType<WetTowel>();
        if (electricalPanel == null) electricalPanel = FindFirstObjectByType<ElectricalPanel>();
        if (fireExtinguisher == null) fireExtinguisher = FindFirstObjectByType<FireExtinguisher>();
        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialUI>();
        if (introCard == null) introCard = GetComponent<RunIntroCard>();
        if (sfx == null)
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
        }

        Current = (RetryRun.Active && LastSetup != null) ? LastSetup : Draw();
        LastSetup = Current;
        Apply(Current);

        DecisionLedger.RecordRunCombo(BuildComboText(Current));
        if (graph != null) graph.OnOriginSelected += OnOriginSelected;
    }

    private void OnDestroy()
    {
        if (graph != null) graph.OnOriginSelected -= OnOriginSelected;
    }

    // ————— 抽签 —————

    private RunSetup Draw()
    {
        var setup = new RunSetup();

        // 规则牌：抽 2/6（加班高峰与独自加班互斥，撞车则重抽后者）
        var rules = new List<RuleCard>((RuleCard[])Enum.GetValues(typeof(RuleCard)));
        setup.rules = new RuleCard[2];
        if (forcedRulesMask != 0)
        {
            int n = 0;
            for (int i = 0; i < rules.Count && n < 2; i++)
                if ((forcedRulesMask & (1 << i)) != 0) setup.rules[n++] = rules[i];
            if (n == 1) setup.rules[1] = rules[(n + 1) % rules.Count];
        }
        else
        {
            for (int i = 0; i < 2; i++)
            {
                int roll = UnityEngine.Random.Range(0, rules.Count);
                setup.rules[i] = rules[roll];
                rules.RemoveAt(roll);
            }
            if (setup.rules[0] == RuleCard.SoloNight && setup.rules[1] == RuleCard.OvertimeCrowd
                || setup.rules[0] == RuleCard.OvertimeCrowd && setup.rules[1] == RuleCard.SoloNight)
            {
                // 互斥：把"独自加班"换成未抽到的第一张检修牌
                for (int i = 0; i < rules.Count; i++)
                {
                    if (rules[i] != RuleCard.OvertimeCrowd && rules[i] != RuleCard.SoloNight)
                    {
                        setup.rules[setup.rules[1] == RuleCard.SoloNight ? 1 : 0] = rules[i];
                        break;
                    }
                }
            }
        }

        // 事件牌：呼救受"加班高峰/独自加班"约束，其余 4 张抽满 2 张
        bool solo = setup.HasRule(RuleCard.SoloNight);
        bool crowd = setup.HasRule(RuleCard.OvertimeCrowd);
        var deck = new List<EventCard> { EventCard.BlockedExit, EventCard.Backdraft, EventCard.LightingFault, EventCard.PhoneLure };
        if (!solo) deck.Insert(0, EventCard.Distress);

        var events = new List<EventCard>();
        if (forcedEventsMask != 0)
        {
            for (int i = 0; i < 5; i++)
                if ((forcedEventsMask & (1 << i)) != 0) events.Add((EventCard)i);
        }
        else
        {
            int drawCount = 2;
            if (crowd && !solo)
            {
                events.Add(EventCard.Distress);   // 加班高峰：呼救必出
                drawCount = 1;
            }
            for (int i = 0; i < drawCount && deck.Count > 0; i++)
            {
                int roll = UnityEngine.Random.Range(0, deck.Count);
                events.Add(deck[roll]);
                deck.RemoveAt(roll);
            }
        }
        setup.events = events.ToArray();

        // 槽位
        setup.alarmSlot = PickSlot(alarmCandidates, forcedAlarmSlot);
        setup.towelSlot = PickSlot(towelCandidates, forcedTowelSlot);
        setup.distressSlot = setup.HasEvent(EventCard.Distress) ? PickSlot(distressDoorCandidates, forcedDistressSlot) : -1;
        setup.blockedSlot = setup.HasEvent(EventCard.BlockedExit) ? PickSlot(blockedExitCandidates, forcedBlockedSlot) : -1;
        setup.backdraftSlot = setup.HasEvent(EventCard.Backdraft) ? PickSlot(backdraftDoorCandidates, forcedBackdraftSlot) : -1;
        setup.lightingSlot = setup.HasEvent(EventCard.LightingFault) ? PickSlot(lightingFaultCandidates, forcedLightingSlot) : -1;
        setup.phoneSlot = setup.HasEvent(EventCard.PhoneLure) ? PickSlot(phoneLureCandidates, forcedPhoneSlot) : -1;

        // 候选门去重：呼救/假出口/背燃绝不落在同一扇门上
        ResolveDoorConflicts(setup);
        return setup;
    }

    /// <summary>事件门冲突消解：同一扇门只能承载一个事件（顺序：呼救→假出口→背燃，后者让路）。</summary>
    private void ResolveDoorConflicts(RunSetup setup)
    {
        var claimed = new List<InteractableDoor>();

        if (setup.distressSlot >= 0 && distressDoorCandidates != null && distressDoorCandidates.Length > 0)
        {
            setup.distressSlot = ClaimFreeSlot(distressDoorCandidates.Length,
                i => distressDoorCandidates[i], claimed, setup.distressSlot);
            var d = distressDoorCandidates[setup.distressSlot];
            if (d != null) claimed.Add(d);
        }
        if (setup.blockedSlot >= 0 && blockedExitCandidates != null && blockedExitCandidates.Length > 0)
        {
            setup.blockedSlot = ClaimFreeSlot(blockedExitCandidates.Length,
                i => blockedExitCandidates[i] != null ? blockedExitCandidates[i].door : null, claimed, setup.blockedSlot);
            var s = blockedExitCandidates[setup.blockedSlot];
            if (s != null && s.door != null) claimed.Add(s.door);
        }
        if (setup.backdraftSlot >= 0 && backdraftDoorCandidates != null && backdraftDoorCandidates.Length > 0)
        {
            setup.backdraftSlot = ClaimFreeSlot(backdraftDoorCandidates.Length,
                i => backdraftDoorCandidates[i], claimed, setup.backdraftSlot);
            var d = backdraftDoorCandidates[setup.backdraftSlot];
            if (d != null) claimed.Add(d);
        }
    }

    private int ClaimFreeSlot(int count, Func<int, InteractableDoor> doorOf, List<InteractableDoor> claimed, int slot)
    {
        if (slot < 0 || slot >= count) return slot;
        var d = doorOf(slot);
        if (d == null || !claimed.Contains(d)) return slot;
        for (int k = 1; k < count; k++)
        {
            int alt = (slot + k) % count;
            var dd = doorOf(alt);
            if (dd != null && !claimed.Contains(dd)) return alt;
        }
        return slot;
    }

    private int PickSlot(Array candidates, int forced)
    {
        if (candidates == null || candidates.Length == 0) return -1;
        if (forced >= 0 && forced < candidates.Length) return forced;
        return UnityEngine.Random.Range(0, candidates.Length);
    }

    // ————— 落地 —————

    private void Apply(RunSetup setup)
    {
        // 规则牌静态标志必须每局重置（历史局抽中过"检修"牌会残留到后续所有局）
        SprinklerSystem.SystemDisabled = false;
        FireShutter.LinkageDisabled = false;

        // 规则牌
        foreach (var r in setup.rules)
        {
            switch (r)
            {
                case RuleCard.PowerMaintenance:
                    if (electricalPanel != null) electricalPanel.InteractGate = () => false;
                    if (panelSeal != null) panelSeal.SetActive(true);
                    break;
                case RuleCard.SprinklerMaintenance:
                    SprinklerSystem.SystemDisabled = true;
                    break;
                case RuleCard.ShutterMaintenance:
                    FireShutter.LinkageDisabled = true;
                    break;
                case RuleCard.ExtinguisherExpired:
                    if (fireExtinguisher != null) fireExtinguisher.ApplyExpiredInspection();
                    if (cabinetTag != null) cabinetTag.SetActive(true);
                    break;
                // OvertimeCrowd/SoloNight 只影响事件抽签
            }
        }

        // 位置抽签
        MoveToSlot(alarmButton != null ? alarmButton.transform : null, alarmCandidates, setup.alarmSlot);
        MoveToSlot(wetTowel != null ? wetTowel.transform : null, towelCandidates, setup.towelSlot);

        // —— 事件接线 ——（固定挂点的拆除已在 Awake 完成）
        // 呼救：抽中再克隆到候选门
        if (setup.distressSlot >= 0 && distressDoorCandidates != null && distressTemplate != null
            && setup.distressSlot < distressDoorCandidates.Length)
        {
            distressTemplate.CloneOnto(distressDoorCandidates[setup.distressSlot]);
        }

        // 假出口：抽中再挂随机门（DoorB 固定件与烘焙牌已在 Awake 拆除）
        if (setup.blockedSlot >= 0 && blockedExitCandidates != null
            && setup.blockedSlot < blockedExitCandidates.Length)
        {
            var slot = blockedExitCandidates[setup.blockedSlot];
            if (slot != null && slot.door != null)
            {
                slot.door.gameObject.AddComponent<BlockedExitDoor>();
                SpawnFakeExitSign(slot.signPos, slot.signEuler);
            }
        }

        // 背燃门
        if (setup.backdraftSlot >= 0 && backdraftDoorCandidates != null
            && setup.backdraftSlot < backdraftDoorCandidates.Length)
        {
            var door = backdraftDoorCandidates[setup.backdraftSlot];
            if (door != null)
            {
                var bd = door.gameObject.AddComponent<BackdraftDoor>();
                if (backdraftBoomClip != null) bd.WireBoomClip(backdraftBoomClip);
            }
        }

        // 照明故障区
        if (setup.lightingSlot >= 0 && lightingFaultCandidates != null
            && setup.lightingSlot < lightingFaultCandidates.Length)
        {
            var lf = lightingFaultCandidates[setup.lightingSlot];
            if (lf != null)
            {
                lf.gameObject.SetActive(true);
                lf.Activate();
            }
        }

        // 手机诱饵
        if (setup.phoneSlot >= 0 && phoneLureCandidates != null
            && setup.phoneSlot < phoneLureCandidates.Length)
        {
            var pl = phoneLureCandidates[setup.phoneSlot];
            if (pl != null) pl.gameObject.SetActive(true);
        }
    }

    private void MoveToSlot(Transform target, Transform[] candidates, int slot)
    {
        if (target == null || candidates == null || slot < 0 || slot >= candidates.Length) return;
        var c = candidates[slot];
        if (c == null) return;
        target.position = c.position;
        target.rotation = c.rotation;
    }

    private void SpawnFakeExitSign(Vector3 pos, Vector3 euler)
    {
        var root = new GameObject("FakeExitSign");
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(euler);

        var backing = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backing.name = "Backing";
        Destroy(backing.GetComponent<Collider>());
        backing.transform.SetParent(root.transform, false);
        backing.transform.localScale = new Vector3(0.62f, 0.3f, 1f);
        var backingMat = new Material(Shader.Find("Sprites/Default"));
        backingMat.color = new Color(0.06f, 0.16f, 0.08f, 0.95f);
        backing.GetComponent<Renderer>().sharedMaterial = backingMat;

        var textGo = new GameObject("ExitText");
        textGo.transform.SetParent(root.transform, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        textGo.transform.localEulerAngles = new Vector3(0f, 180f, 0f);
        var tm = textGo.AddComponent<TextMesh>();
        tm.text = "EXIT";
        tm.fontSize = 48;
        tm.characterSize = 0.055f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.35f, 1f, 0.5f);
        var tmr = textGo.GetComponent<MeshRenderer>();
        if (tmr != null)
        {
            // 文字用无光照恒亮（假牌要显眼）
            var tmMat = new Material(Shader.Find("Sprites/Default"));
            tmMat.color = new Color(0.35f, 1f, 0.5f, 1f);
            tmr.sharedMaterial = tmMat;
        }
    }

    // ————— 点火演出与组合卡 —————

    private void OnOriginSelected()
    {
        if (ignitionPlayed || graph == null) return;
        ignitionPlayed = true;
        StartCoroutine(IgnitionCinematic());
    }

    /// <summary>点火演出：等开场黑幕开始淡出后——爆响+起火点火球+轻震+组合卡。</summary>
    private IEnumerator IgnitionCinematic()
    {
        // 与开场黑幕错开：演出必须看得见（黑幕保持~1s 的节奏配置下 1.3s 正好在淡出中段）
        float delay = 1.3f - Time.timeSinceLevelLoad;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (sfx != null && boomClip != null) sfx.PlayOneShot(boomClip, 0.95f);

        // 火球：起火点一次爆开的橙色迸发（取当前燃着的起源火位置）
        FirePoint fp = null;
        foreach (var f in FindObjectsByType<FirePoint>(FindObjectsSortMode.None))
            if (f != null && f.IsBurning) { fp = f; break; }
        if (fp != null) StartCoroutine(FireballRoutine(fp.transform.position));
        StartCoroutine(ShakeRoutine(0.28f, 0.03f));

        // 开局组合卡（起火点 + 规则牌；事件保持悬念）
        if (introCard != null)
        {
            string rules = Current != null && Current.rules != null && Current.rules.Length > 0
                ? string.Join(" ｜ ", System.Array.ConvertAll(Current.rules, r => RuleNames[(int)r]))
                : "";
            introCard.Show($"本场火情：{graph.ActiveOriginLabel}", rules, 3.2f);
        }
    }

    private IEnumerator FireballRoutine(Vector3 pos)
    {
        var root = new GameObject("IgnitionBurst");
        root.transform.position = pos + Vector3.up * 0.5f;
        root.SetActive(false);   // AddComponent 的 PS 会立即自动播放——配置期间必须停用
        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1.1f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        main.maxParticles = 110;
        main.gravityModifier = -0.15f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.62f, 0.15f), new Color(1f, 0.28f, 0.05f));

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 110f) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.65f, 0.18f), 0f), new GradientColorKey(new Color(0.75f, 0.12f, 0.02f), 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.55f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = root.GetComponent<ParticleSystemRenderer>();
        if (fireMaterial != null) renderer.material = fireMaterial;
        else
        {
            var mat = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            renderer.material = mat;
        }
        renderer.sortingFudge = -8;

        root.SetActive(true);
        yield return new WaitForSeconds(1.6f);
        Destroy(root);
    }

    private IEnumerator ShakeRoutine(float seconds, float magnitude)
    {
        var cam = Camera.main;
        if (cam == null) yield break;
        var original = cam.transform.localPosition;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float fade = 1f - t / seconds;
            cam.transform.localPosition = original + new Vector3(
                (UnityEngine.Random.value - 0.5f) * 2f * magnitude * fade,
                (UnityEngine.Random.value - 0.5f) * 2f * magnitude * fade, 0f);
            yield return null;
        }
        cam.transform.localPosition = original;
    }

    private string BuildComboText(RunSetup setup)
    {
        var sb = new System.Text.StringBuilder();
        if (setup != null && setup.rules != null)
        {
            var names = new List<string>();
            foreach (var r in setup.rules) names.Add(RuleNames[(int)r]);
            if (names.Count > 0) sb.Append(string.Join("、", names));
        }
        return sb.ToString();
    }
}
