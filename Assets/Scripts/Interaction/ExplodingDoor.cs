using System.Collections;
using UnityEngine;

/// <summary>
/// 炸门演出（办公楼·仓库）：仓库起火点被选中并与导演点火演出同步时，仓库门被爆燃气浪直接炸飞——
/// 门叶 AddComponent&lt;Rigidbody&gt; 冲出门框（向外+向上+随机扭矩），落地后冻结为地面残骸；
/// 同步门洞火球迸发+尘雾+音爆+近距玩家伤害+震屏。门炸飞后：InteractableDoor 停用、
/// InteractGate=false（不可交互）、ForceSetOpen(true) 打开图阀门（门洞敞开=火烟通道）。
/// 非仓库起火的局：门保持完好、正常交互。
/// </summary>
[RequireComponent(typeof(InteractableDoor))]
public class ExplodingDoor : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private FireZoneGraph graph;
    [SerializeField] private Transform leaf;               // 门叶（物理飞出）
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private TutorialUI tutorial;
    [SerializeField] private AudioClip boomClip;           // BackdraftBoom.wav
    [SerializeField] private Material fireMaterial;        // M_SoftFire

    [Header("参数")]
    [Tooltip("仓库起火点下标（graph.origins 内下标）")]
    [SerializeField] private int originIndex = 5;
    [Tooltip("与导演点火演出同步的延迟秒数")]
    [SerializeField] private float blastDelay = 1.3f;
    [Tooltip("门叶飞出的世界方向（朝前台/南区）")]
    [SerializeField] private Vector3 blastDir = new Vector3(0f, 0f, -1f);
    [SerializeField] private float impulse = 7.5f;
    [SerializeField] private float lift = 3.5f;
    [SerializeField, Range(0f, 100f)] private float damage = 30f;
    [SerializeField] private float damageRadius = 4.5f;

    public bool Exploded { get; private set; }

    private void Awake()
    {
        if (graph == null) graph = FindFirstObjectByType<FireZoneGraph>();
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (graph != null) graph.OnOriginSelected += OnOriginSelected;
    }

    private void OnDisable()
    {
        if (graph != null) graph.OnOriginSelected -= OnOriginSelected;
    }

    private void OnOriginSelected()
    {
        if (Exploded || graph.ActiveOriginIndex != originIndex) return;
        StartCoroutine(BlastRoutine());
    }

    private IEnumerator BlastRoutine()
    {
        yield return new WaitForSeconds(blastDelay);
        Exploded = true;

        var door = GetComponent<InteractableDoor>();
        Vector3 doorwayPos = transform.position + Vector3.up * 1.1f;

        // 0) 根节点碰撞体全部禁用（触发盒+静态关门拦截块——拦截块不拆会把门叶卡死在门框体积里）
        foreach (var col in GetComponents<Collider>()) col.enabled = false;

        // 1) 门叶脱离父级成自由刚体（脱离门框体积，物理不被嵌套碰撞干扰）
        leaf.SetParent(null);
        var rb = leaf.gameObject.AddComponent<Rigidbody>();
        rb.mass = 22f;
        rb.velocity = blastDir * impulse + Vector3.up * lift;
        rb.angularVelocity = new Vector3(Random.Range(-6f, 6f), Random.Range(-10f, 10f), Random.Range(-8f, 8f));

        // 2) 门洞敞开（图阀门）+ 门体停用（不可交互、停铰链动画）
        door.ForceSetOpen(true);
        door.InteractGate = () => false;
        door.enabled = false;

        // 3) 门洞火球迸发（朝门外）
        var root = new GameObject("WarehouseDoorBlast");
        root.SetActive(false);
        root.transform.position = doorwayPos + blastDir * 0.5f;
        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1.1f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.7f);
        main.maxParticles = 130;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.6f, 0.15f), new Color(1f, 0.25f, 0.05f));
        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 130f) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;
        var colMod = ps.colorOverLifetime;
        colMod.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.62f, 0.18f), 0f), new GradientColorKey(new Color(0.75f, 0.12f, 0.02f), 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.55f, 0.5f), new GradientAlphaKey(0f, 1f) });
        colMod.color = new ParticleSystem.MinMaxGradient(grad);
        var renderer = root.GetComponent<ParticleSystemRenderer>();
        if (fireMaterial != null) renderer.material = fireMaterial;
        else renderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        renderer.sortingFudge = -8;
        root.SetActive(true);
        Destroy(root, 2.0f);

        // 4) 音爆+尘雾
        if (boomClip != null) AudioSource.PlayClipAtPoint(boomClip, doorwayPos, 1f);
        DustPuff(doorwayPos + Vector3.up * 0.3f);

        // 5) 近距玩家伤害+震屏
        var player = playerHealth != null ? playerHealth.transform : null;
        if (playerHealth != null && player != null
            && Vector3.Distance(player.position, doorwayPos) <= damageRadius)
        {
            playerHealth.ApplyDamage(damage, "仓库爆燃冲击");
            StartCoroutine(ShakeRoutine(0.4f, 0.05f));
        }

        // 6) 残骸冻结（2.5s后钉在落点，平躺微翘）
        yield return new WaitForSeconds(2.5f);
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            // 平躺姿态：绕X翻90°让门叶长边放平（面板2.1m的本地Y轴转为水平）+随机朝向
            leaf.rotation = Quaternion.Euler(90f + Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
            Physics.SyncTransforms();
            var leafRend = leaf.GetComponentInChildren<Renderer>();
            if (leafRend != null)
            {
                float sink = leafRend.bounds.min.y - 0.03f;
                if (sink > 0f) leaf.position += Vector3.down * sink;
            }
            else
            {
                var lp = leaf.position;
                leaf.position = new Vector3(lp.x, 0.05f, lp.z);
            }
        }
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
                (Random.value - 0.5f) * 2f * magnitude * fade,
                (Random.value - 0.5f) * 2f * magnitude * fade, 0f);
            yield return null;
        }
        cam.transform.localPosition = original;
    }

    private static void DustPuff(Vector3 center)
    {
        var root = new GameObject("BlastDust");
        root.transform.position = center;
        root.SetActive(false);
        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1.2f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.gravityModifier = -0.05f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.55f, 0.53f, 0.5f, 0.5f), new Color(0.65f, 0.62f, 0.58f, 0.35f));
        main.maxParticles = 18;
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
        var renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
        root.SetActive(true);
        ps.Play();
        Destroy(root, 2.2f);
    }
}
