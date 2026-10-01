using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 轰燃吊顶坍塌（办公楼终局事件）：某区火势首次逼近轰燃（图 OnZoneCritical）时，
/// 该区头顶的吊顶板物理掉落+碎裂闷响+尘雾——轰燃不只是红字警告，还有物理威胁感。
/// 吊顶板是编辑模式预置的薄盒（初始隐藏），掉落后落在地上成为无害碎块（动态物体受火光实时照明）。
/// </summary>
public class CeilingCollapse : MonoBehaviour
{
    [System.Serializable]
    public class PanelGroup
    {
        public int zoneIndex;
        public GameObject[] panels;      // 初始 inactive 的吊顶板
        public GameObject[] ceilingTiles; // 掉落点对应的天花板瓦片（轰燃时隐藏=破洞——洞与掉落物的因果在这里闭合）
        public AudioSource crashAudio;   // 闷响（挂本组附近）
    }

    [SerializeField] private List<PanelGroup> groups = new List<PanelGroup>();
    [SerializeField] private FireZoneGraph graph;

    private static Material panelMat;

    private void OnEnable()
    {
        if (graph == null) graph = FindFirstObjectByType<FireZoneGraph>();
        if (graph != null) graph.OnZoneCritical += OnCritical;
    }

    private void OnDisable()
    {
        if (graph != null) graph.OnZoneCritical -= OnCritical;
    }

    private void OnCritical(int zoneIndex)
    {
        foreach (var g in groups)
        {
            if (g == null || g.zoneIndex != zoneIndex || g.panels == null) continue;
            // 先开洞（隐藏天花板瓦片），再让板从洞里掉出来
            if (g.ceilingTiles != null)
                foreach (var tile in g.ceilingTiles)
                    if (tile != null) tile.SetActive(false);
            foreach (var p in g.panels)
            {
                if (p == null) continue;
                StartCoroutine(DropRoutine(p, g.crashAudio));
            }
        }
    }

    private IEnumerator DropRoutine(GameObject panel, AudioSource crash)
    {
        panel.SetActive(true);
        var rb = panel.GetComponent<Rigidbody>();
        if (rb == null) rb = panel.AddComponent<Rigidbody>();
        // 轻微随机侧抛：掉落不是整齐的下坠，是失稳的散落
        rb.velocity = new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f));
        rb.angularVelocity = new Vector3(Random.Range(-2f, 2f), Random.Range(-2f, 2f), Random.Range(-2f, 2f));

        if (crash != null && !crash.isPlaying) crash.Play();

        // 尘雾：一次性 12 粒灰色 puff（程序化贴图，快速消散）
        DustPuff(panel.transform.position);

        // 落地冻结：约 1.5s 后停住成为地面碎块（0.06 高，不绊人）
        yield return new WaitForSeconds(1.6f);
        if (rb != null)
        {
            rb.isKinematic = true;
            var t = panel.transform;
            Vector3 pos = t.position;
            pos.y = Mathf.Max(0.03f, pos.y < 0.25f ? 0.03f : pos.y);
            t.position = pos;
            // 平躺微翘：翻滚姿态保留会像"悬空的怪板"——矫正为贴地姿态（保留明显倾角+随机朝向）
            t.rotation = Quaternion.Euler(
                Random.Range(-22f, 22f), Random.Range(0f, 360f), Random.Range(-22f, 22f));
            // 追加小碎块：一块整板像"文件盒"，一摊碎渣才是坍塌
            SpawnDebris(pos);
        }
    }

    /// <summary>掉落点周围散落 3 个小碎块（无光照恒白，火光染不橙）。</summary>
    private static void SpawnDebris(Vector3 center)
    {
        if (debrisMat == null)
        {
            debrisMat = new Material(Shader.Find("Unlit/Color"));
            debrisMat.color = new Color(0.88f, 0.86f, 0.80f);
        }
        for (int i = 0; i < 3; i++)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
            d.name = "CeilingDebris";
            Object.Destroy(d.GetComponent<Collider>());   // 碎渣不做碰撞，纯视觉
            d.transform.position = center + new Vector3(
                Random.Range(-0.8f, 0.8f), Random.Range(0.08f, 0.25f), Random.Range(-0.8f, 0.8f));
            d.transform.rotation = Quaternion.Euler(Random.Range(-35f, 35f), Random.Range(0f, 360f), Random.Range(-35f, 35f));
            d.transform.localScale = new Vector3(Random.Range(0.30f, 0.62f), Random.Range(0.04f, 0.08f), Random.Range(0.30f, 0.62f));
            d.GetComponent<Renderer>().sharedMaterial = debrisMat;
        }
    }

    private static Material debrisMat;

    private static void DustPuff(Vector3 center)
    {
        var root = new GameObject("DustPuff");
        root.transform.position = center + Vector3.up * 0.6f;
        root.SetActive(false);   // AddComponent 的 PS 会自动播放——配置期间必须停用（duration 被拒坑）
        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1.2f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
        main.gravityModifier = -0.05f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.55f, 0.53f, 0.5f, 0.5f), new Color(0.65f, 0.62f, 0.58f, 0.35f));
        main.maxParticles = 16;
        var shp = ps.shape;
        shp.shapeType = ParticleSystemShapeType.Sphere;
        shp.radius = 0.4f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.6f, 0.58f, 0.55f), 0f), new GradientColorKey(new Color(0.6f, 0.58f, 0.55f), 1f) },
            new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0.0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);
        var sizeOver = ps.sizeOverLifetime;
        sizeOver.enabled = true;
        sizeOver.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(0.4f, 1.2f), new Keyframe(1f, 2.2f)));
        var renderer = root.GetComponent<ParticleSystemRenderer>();
        if (panelMat == null)
        {
            panelMat = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
        }
        renderer.material = panelMat;
        root.SetActive(true);
        ps.Play();
        Destroy(root, 2.2f);
    }
}
