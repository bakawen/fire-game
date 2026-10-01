using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 喷淋系统（办公楼设施联动）：热激活而非报警联动——区火势越过阈值时，该区喷淋头自动爆开：
/// 顶部水雾粒子+水声循环+该区火增长×0.55 压制（FireZoneGraph.SetZoneSprinkler）。
/// 喷淋不清烟（教学：它在为你争取疏散时间，不是在灭火）。
/// 喷淋头为运行时程序化小圆柱（动态物件）+ 向下水雾粒子；场景只预置"喷淋头锚点"空物体。
/// 规则牌"水系统检修"（SprinklerMaintenance）→ SystemDisabled=true，永不启动。
/// </summary>
public class SprinklerSystem : MonoBehaviour
{
    [System.Serializable]
    public class ZoneGroup
    {
        public int zoneIndex;
        [Tooltip("该区喷淋头锚点（场景预置空物体，位于天花板下方）")]
        public Transform[] heads;
    }

    [Header("引用")]
    [SerializeField] private FireZoneGraph graph;
    [SerializeField] private List<ZoneGroup> groups = new List<ZoneGroup>();

    [Header("参数")]
    [Tooltip("区火势越过此值→该区喷淋启动")]
    [SerializeField] private float activateThreshold = 0.40f;
    [SerializeField] private AudioClip waterClip;
    [SerializeField, TextArea(1, 2)] private string activateBroadcast = "「{0}」喷淋启动——它在为你争取时间，不是在灭火。撤离！";
    [SerializeField] private TutorialUI tutorial;

    /// <summary>规则牌联动：水系统检修中（本局喷淋永不启动）。</summary>
    public static bool SystemDisabled;

    private readonly HashSet<int> activeZones = new HashSet<int>();
    private bool firstBroadcastDone;
    private static Material headMat;

    public bool IsZoneActive(int zoneIndex) => activeZones.Contains(zoneIndex);
    public int GroupCount => groups != null ? groups.Count : 0;

    private void Awake()
    {
        if (graph == null) graph = FindFirstObjectByType<FireZoneGraph>();
        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialUI>();
        if (headMat == null)
        {
            headMat = new Material(Shader.Find("Standard"));
            headMat.color = new Color(0.45f, 0.46f, 0.5f);
            headMat.SetFloat("_Metallic", 0.8f);
            headMat.SetFloat("_Glossiness", 0.4f);
        }
        if (groups == null) return;
        foreach (var g in groups)
        {
            if (g == null || g.heads == null) continue;
            foreach (var h in g.heads)
            {
                if (h == null) continue;
                BuildHead(h);
            }
        }
    }

    private void Update()
    {
        if (graph == null || SystemDisabled || groups == null) return;
        foreach (var g in groups)
        {
            if (g == null || g.zoneIndex < 0 || activeZones.Contains(g.zoneIndex)) continue;
            if (graph.GetZoneFire(g.zoneIndex) >= activateThreshold)
            {
                activeZones.Add(g.zoneIndex);
                ActivateGroup(g);
            }
        }
    }

    private void ActivateGroup(ZoneGroup g)
    {
        graph.SetZoneSprinkler(g.zoneIndex, true);
        DecisionLedger.RecordSprinklerActivated(graph.GetZoneId(g.zoneIndex));

        if (g.heads == null) return;
        foreach (var h in g.heads)
        {
            if (h == null) continue;
            var spray = h.transform.Find("WaterSpray");
            if (spray != null) spray.gameObject.SetActive(true);   // playOnAwake=true：激活即喷淋
        }

        if (waterClip != null)
        {
            var center = g.heads != null && g.heads.Length > 0 && g.heads[0] != null ? g.heads[0].position : transform.position;
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = waterClip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 4f;
            src.maxDistance = 20f;
            src.volume = 0.7f;
            src.transform.position = center;
            src.Play();
        }

        // 每局只广播一次（多区连动时不刷屏）
        if (!firstBroadcastDone && tutorial != null)
        {
            firstBroadcastDone = true;
            tutorial.ShowHint(activateBroadcast.Replace("{0}", graph.GetZoneId(g.zoneIndex)));
        }
    }

    /// <summary>程序化喷淋头：小圆柱体+向下的水雾粒子（挂在锚点下）。</summary>
    private void BuildHead(Transform anchor)
    {
        var head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        head.name = "SprinklerHead";
        Destroy(head.GetComponent<Collider>());
        head.transform.SetParent(anchor, false);
        head.transform.localPosition = Vector3.zero;
        head.transform.localScale = new Vector3(0.14f, 0.05f, 0.14f);
        head.GetComponent<Renderer>().sharedMaterial = headMat;

        var psGo = new GameObject("WaterSpray");
        psGo.transform.SetParent(anchor, false);
        psGo.transform.localPosition = new Vector3(0f, -0.06f, 0f);
        psGo.SetActive(false);   // AddComponent 的 PS 自动播放坑：配置期间停用

        var ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.gravityModifier = 1.1f;
        main.maxParticles = 220;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.72f, 0.82f, 0.95f, 0.65f), new Color(0.85f, 0.92f, 1f, 0.45f));

        var emission = ps.emission;
        emission.rateOverTime = 120f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 24f;
        shape.radius = 0.04f;
        shape.rotation = new Vector3(90f, 0f, 0f);   // 锥口朝下

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.8f, 0.88f, 1f), 0f), new GradientColorKey(new Color(0.7f, 0.82f, 0.95f), 1f) },
            new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.4f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = psGo.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
        renderer.sortingFudge = -3;

        psGo.SetActive(false);   // 待区激活才播
    }
}
