using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 终局硬死线（办公楼）：任一区火势首次越过轰燃临界（图 OnZoneCritical）→ 结构失效倒计时启动。
/// 时长按当时全楼总火势折算（处置越好火势越低，死线越宽）：baseSeconds + (1-火势)*bonusSeconds。
/// 倒计时内每四分之一封死一段预定走廊（横梁碎块掉落成路障+尘雾+闷响+震屏）；
/// 归零=死于结构坍塌。通关时 WinTrigger.OnWin 记录"死线余量"。
/// 与 OfficeFlashover 协同：轰燃警告（红字警笛震屏）仍由它负责，本组件管倒计时与物理封路。
/// </summary>
public class StructuralFailure : MonoBehaviour
{
    [System.Serializable]
    public class SealPoint
    {
        [Tooltip("封路锚点（横梁在此掉落，封住这段走廊）")]
        public Transform anchor;
        [Tooltip("横梁尺寸（默认跨走廊）")]
        public Vector3 size = new Vector3(4.2f, 0.5f, 1.4f);
        [Tooltip("本段位置名（记账文案用，可空）")]
        public string label = "";
    }

    [Header("引用")]
    [SerializeField] private FireZoneGraph graph;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private StructuralCountdownUI countdownUI;
    [SerializeField] private WinTrigger winTrigger;

    [Header("封路点（按倒计时四等分顺序掉落；每点须留有替代路线）")]
    [SerializeField] private List<SealPoint> sealPoints = new List<SealPoint>();

    [Header("参数")]
    [SerializeField] private float baseSeconds = 20f;
    [SerializeField] private float bonusSeconds = 16f;
    [SerializeField] private AudioClip crashClip;
    [SerializeField, TextArea(2, 4)] private string collapseDeathDetail =
        "轰燃之后，建筑结构失去完整性的时间只有几十秒。\n天花板与横梁开始成段坍塌——这一次，你没能赶在结构失效之前离开。\n\n记住：越过轰燃临界，撤离优先级高于一切。";

    public bool Running { get; private set; }
    public float Remaining { get; private set; }

    private float duration;
    private float elapsedAtStart;
    private int sealsDone;
    private bool killed;
    private float graphElapsedAtStart;

    private void Start()
    {
        if (graph == null) graph = FindFirstObjectByType<FireZoneGraph>();
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (winTrigger == null) winTrigger = FindFirstObjectByType<WinTrigger>();
        if (countdownUI == null) countdownUI = GetComponent<StructuralCountdownUI>();
        if (graph != null) graph.OnZoneCritical += OnZoneCritical;
        if (winTrigger != null) winTrigger.OnWin += OnWin;
    }

    private void OnDestroy()
    {
        if (graph != null) graph.OnZoneCritical -= OnZoneCritical;
        if (winTrigger != null) winTrigger.OnWin -= OnWin;
    }

    private void OnZoneCritical(int zoneIndex)
    {
        if (Running || killed) return;
        StartCountdown();
    }

    private void StartCountdown()
    {
        Running = true;
        elapsedAtStart = Time.timeSinceLevelLoad;
        graphElapsedAtStart = graph != null ? graph.Elapsed : 0f;

        // 死线时长：全楼火势越重，结构撑得越短（处置好=火势低=时间多）
        float fireNorm = graph != null
            ? Mathf.Clamp01(graph.TotalFire / Mathf.Max(1f, graph.ZoneCount * 0.6f))
            : 0.6f;
        duration = baseSeconds + (1f - fireNorm) * bonusSeconds;
        Remaining = duration;
        sealsDone = 0;

        if (countdownUI != null) countdownUI.Begin();
        DecisionLedger.RecordCollapseCountdown(duration);
    }

    private void Update()
    {
        if (!Running || killed || Time.timeScale <= 0.01f) return;

        Remaining -= Time.deltaTime;

        // 四等分逐段封路
        float progress = (duration - Remaining) / duration;
        int dueSeals = Mathf.FloorToInt(progress * 4f);
        while (sealsDone < dueSeals && sealPoints != null && sealsDone < sealPoints.Count)
        {
            var p = sealPoints[sealsDone];
            if (p != null && p.anchor != null)
                StartCoroutine(SealRoutine(p));
            sealsDone++;
        }

        if (countdownUI != null) countdownUI.SetRemaining(Mathf.Max(0f, Remaining));

        if (Remaining <= 0f)
        {
            killed = true;
            if (countdownUI != null) countdownUI.Stop();
            DecisionLedger.RecordCollapseDeath();
            if (playerHealth != null) playerHealth.DieByCollapse(collapseDeathDetail);
        }
    }

    private void OnWin()
    {
        if (Running && !killed)
        {
            DecisionLedger.RecordCollapseEscaped(Remaining);
            if (countdownUI != null) countdownUI.Stop();
        }
    }

    /// <summary>横梁封路：从天花板坠落→落地成路障（带碰撞体挡路）+尘雾+闷响+震屏。</summary>
    private IEnumerator SealRoutine(SealPoint p)
    {
        Vector3 pos = p.anchor.position;
        Quaternion rot = p.anchor.rotation;

        var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "CollapseBeam_" + p.label;
        beam.transform.localScale = p.size;
        beam.transform.rotation = rot;
        beam.transform.position = pos + Vector3.up * 3.2f;
        beam.GetComponent<Renderer>().sharedMaterial = BeamMaterial();

        var col = beam.GetComponent<BoxCollider>();
        col.enabled = false;

        // 坠落（半物理：加速下坠更沉）
        Vector3 target = pos + Vector3.up * (p.size.y * 0.5f);
        float t = 0f;
        while (t < 0.55f)
        {
            t += Time.deltaTime;
            float k = t / 0.55f;
            beam.transform.position = Vector3.Lerp(pos + Vector3.up * 3.2f, target, k * k);
            yield return null;
        }
        beam.transform.position = target;
        col.enabled = true;

        if (crashClip != null)
        {
            AudioSource.PlayClipAtPoint(crashClip, pos, 1f);
        }
        DustPuff(pos + Vector3.up * 0.6f);
        DecisionLedger.RecordCorridorSealed(p.label);
        StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        var cam = Camera.main;
        if (cam == null) yield break;
        var original = cam.transform.localPosition;
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            float fade = 1f - t / 0.4f;
            cam.transform.localPosition = original + new Vector3(
                (UnityEngine.Random.value - 0.5f) * 2f * 0.05f * fade,
                (UnityEngine.Random.value - 0.5f) * 2f * 0.05f * fade, 0f);
            yield return null;
        }
        cam.transform.localPosition = original;
    }

    private static Material beamMat;
    private static Material BeamMaterial()
    {
        if (beamMat == null)
        {
            beamMat = new Material(Shader.Find("Standard"));
            beamMat.color = new Color(0.30f, 0.29f, 0.27f);
            beamMat.SetFloat("_Metallic", 0.35f);
            beamMat.SetFloat("_Glossiness", 0.15f);
        }
        return beamMat;
    }

    /// <summary>简易尘雾（一次性 12 粒灰 puff）。</summary>
    private static void DustPuff(Vector3 center)
    {
        var root = new GameObject("CollapseDust");
        root.transform.position = center;
        root.SetActive(false);
        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1.2f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.gravityModifier = -0.05f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.55f, 0.53f, 0.5f, 0.5f), new Color(0.65f, 0.62f, 0.58f, 0.35f));
        main.maxParticles = 16;
        var shp = ps.shape;
        shp.shapeType = ParticleSystemShapeType.Sphere;
        shp.radius = 0.5f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.6f, 0.58f, 0.55f), 0f), new GradientColorKey(new Color(0.6f, 0.58f, 0.55f), 1f) },
            new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);
        var sizeOver = ps.sizeOverLifetime;
        sizeOver.enabled = true;
        sizeOver.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(0.4f, 1.2f), new Keyframe(1f, 2.2f)));
        var renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
        root.SetActive(true);
        ps.Play();
        Destroy(root, 2.2f);
    }
}
