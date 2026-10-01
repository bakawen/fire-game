using System.Collections;
using UnityEngine;

/// <summary>
/// 背燃门（办公楼事件牌）：导演抽牌后挂到随机候选门上——这扇门后蓄着高温浓烟。
/// 玩家靠近自动提示"把手发烫"；按 E 先是摸门柄（烫手警告：回燃风险，换路），
/// 再按 E = 强行开门 → 火球喷出（重创不致死）+ 音爆 + 震屏，门被热浪顶死不再可开。
/// 教育点：开门前先摸门柄/看烟——门后蓄热房间开缝即回燃（backdraft）。
/// 与 QuizDoor/BlockedExitDoor 一样走 OpenInterceptor 模式；导演保证候选门不与答题门重复。
/// </summary>
[RequireComponent(typeof(InteractableDoor))]
public class BackdraftDoor : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private TutorialUI tutorial;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private AudioClip boomClip;
    [SerializeField] private Material fireMaterial;   // 留空=运行时生成 Additive 柔光材质
    [SerializeField] private Transform player;

    [Header("参数")]
    [SerializeField] private float proximityRange = 2.2f;
    [SerializeField, Range(0f, 100f)] private float blastDamage = 35f;

    [Header("文案")]
    [SerializeField, TextArea(1, 2)] private string proximityHint = "这扇门的金属把手在发烫——门后可能蓄着火，开门前先按 E 摸一摸。";
    [SerializeField, TextArea(2, 3)] private string touchedHint = "烫手！门后蓄着高温浓烟——开一条缝就是回燃！换一条路走，别碰这扇门。";
    [SerializeField, TextArea(2, 3)] private string blownHint = "回燃！高温烟气遇氧瞬间爆燃——强行开门就是引火烧身。\n记住：摸门柄发烫的门，永远不要打开。";

    public enum State { Armed, Warned, Blown }
    public State CurrentState { get; private set; } = State.Armed;

    /// <summary>导演接线：回燃音爆剪辑。</summary>
    public void WireBoomClip(AudioClip clip) => boomClip = clip;

    private InteractableDoor door;
    private bool proximityHinted;
    private static Texture2D glowTex;

    private void Awake()
    {
        door = GetComponent<InteractableDoor>();
        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialUI>();
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (player == null)
        {
            var pi = FindFirstObjectByType<PlayerInteraction>();
            if (pi != null) player = pi.transform;
        }
        door.OpenInterceptor = p => { HandlePress(); return true; };
    }

    private void Update()
    {
        if (CurrentState != State.Armed || player == null || proximityHinted) return;
        if (Time.timeScale <= 0.01f) return;
        if (Vector3.Distance(player.position, transform.position) <= proximityRange)
        {
            proximityHinted = true;
            if (tutorial != null) tutorial.ShowHint(proximityHint);
        }
    }

    /// <summary>摸门柄（第一次 E）→ 警告；强行开门（第二次 E）→ 回燃爆发。</summary>
    private void HandlePress()
    {
        if (CurrentState == State.Armed)
        {
            CurrentState = State.Warned;
            DecisionLedger.RecordBackdraftChecked();
            if (tutorial != null) tutorial.ShowHint(touchedHint);
        }
        else if (CurrentState == State.Warned)
        {
            CurrentState = State.Blown;
            door.InteractGate = () => false;   // 门被热浪顶死，不再可交互（InteractGate=false=封禁）
            DecisionLedger.RecordBackdraftBlown();
            if (tutorial != null) tutorial.ShowHint(blownHint);
            StartCoroutine(BlastRoutine());
        }
    }

    private IEnumerator BlastRoutine()
    {
        // 火球：门缝向外的橙色爆喷
        Vector3 pos = transform.position + Vector3.up * 1.1f;
        var playerDir = player != null
            ? (player.position - pos).normalized
            : Vector3.forward;

        var root = new GameObject("BackdraftBlast");
        root.transform.position = pos;
        root.transform.rotation = Quaternion.LookRotation(playerDir);
        root.SetActive(false);   // AddComponent 的 PS 会立即自动播放——配置期间必须停用
        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.9f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.7f);
        main.maxParticles = 90;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.55f, 0.12f), new Color(1f, 0.25f, 0.05f));

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 90f) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.25f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.6f, 0.15f), 0f), new GradientColorKey(new Color(0.7f, 0.1f, 0.02f), 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.material = fireMaterial != null ? fireMaterial : RuntimeFireMaterial();
        renderer.sortingFudge = -8;

        root.SetActive(true);

        if (boomClip != null) AudioSource.PlayClipAtPoint(boomClip, pos, 1f);

        if (playerHealth != null && player != null
            && Vector3.Distance(player.position, pos) < 6f)
        {
            playerHealth.ApplyDamage(blastDamage, "回燃冲击");
        }

        // 震屏
        var cam = Camera.main;
        if (cam != null)
        {
            var original = cam.transform.localPosition;
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float fade = 1f - t / 0.35f;
                cam.transform.localPosition = original + new Vector3(
                    (Random.value - 0.5f) * 2f * 0.06f * fade,
                    (Random.value - 0.5f) * 2f * 0.06f * fade, 0f);
                yield return null;
            }
            cam.transform.localPosition = original;
        }

        Destroy(root, 1.6f);
    }

    private static Material RuntimeFireMaterial()
    {
        var mat = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        if (glowTex == null)
        {
            const int S = 64;
            glowTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(S / 2f, S / 2f)) / (S / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    glowTex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            glowTex.Apply();
        }
        mat.mainTexture = glowTex;
        return mat;
    }
}
