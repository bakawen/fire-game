using UnityEngine;

/// <summary>
/// 遗留手机警报（办公楼事件牌）：某桌面手机持续响铃（纯音频+小道具诱饵），
/// 教育点=火场里不回头拿财物、不为财物折返。起火后开始响，接近=被吸引折返（复盘负评），
/// 45s 后自行停响（没人接）。导演抽牌后激活对应候选点。
/// </summary>
public class PhoneLure : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private AudioClip ringClip;
    [SerializeField] private AudioSource ringSrc;
    [SerializeField] private Transform player;
    [SerializeField] private TutorialUI tutorial;
    [SerializeField] private FireZoneGraph graph;

    [Header("参数")]
    [SerializeField] private float ringStopSeconds = 45f;
    [SerializeField, Range(0.5f, 6f)] private float approachRange = 1.6f;
    [SerializeField] private float audibleRange = 16f;

    [Header("文案")]
    [SerializeField, TextArea(1, 2)] private string approachHint = "手机还在响……别为财物折返——火场里最贵的是时间。";

    private bool ringing;
    private bool lured;
    private bool silenced;
    private float t;

    private void Awake()
    {
        if (player == null)
        {
            var pi = FindFirstObjectByType<PlayerInteraction>();
            if (pi != null) player = pi.transform;
        }
        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialUI>();
        if (graph == null) graph = FindFirstObjectByType<FireZoneGraph>();
        if (ringSrc == null)
        {
            ringSrc = gameObject.AddComponent<AudioSource>();
            ringSrc.playOnAwake = false;
            ringSrc.spatialBlend = 1f;
            ringSrc.rolloffMode = AudioRolloffMode.Linear;
            ringSrc.minDistance = 2.5f;
            ringSrc.maxDistance = audibleRange;
            ringSrc.volume = 0.8f;
        }
        BuildProp();
    }

    private void OnEnable()
    {
        if (graph != null) graph.OnOriginSelected += OnIgnited;
    }

    private void OnDisable()
    {
        if (graph != null) graph.OnOriginSelected -= OnIgnited;
    }

    private void OnIgnited() => ringing = true;

    private void Update()
    {
        if (Time.timeScale <= 0.01f) return;

        if (ringing)
        {
            t += Time.deltaTime;
            if (t >= ringStopSeconds || lured)
            {
                ringing = false;
                if (ringSrc != null && ringSrc.isPlaying) ringSrc.Stop();
                return;
            }
            // 响铃节奏：2s 响 / 4s 静（无剪辑时静默——仅靠视觉与提示牌存在）
            float phase = t % 6f;
            bool inRing = phase < 2f;
            if (ringSrc != null && ringClip != null)
            {
                if (inRing && !ringSrc.isPlaying) ringSrc.Play();
                if (!inRing && ringSrc.isPlaying) ringSrc.Stop();
            }
        }

        if (!lured && player != null && Vector3.Distance(player.position, transform.position) <= approachRange)
        {
            lured = true;
            ringing = false;
            if (ringSrc != null && ringSrc.isPlaying) ringSrc.Stop();
            DecisionLedger.RecordPhoneLureApproached();
            if (tutorial != null) tutorial.ShowHint(approachHint);
        }
    }

    /// <summary>程序化手机道具：深色小盒+自发光屏幕（桌面摆件）。</summary>
    private void BuildProp()
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "PhoneBody";
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        body.transform.localRotation = Quaternion.Euler(0f, 37f, 0f);
        body.transform.localScale = new Vector3(0.18f, 0.03f, 0.09f);
        var bodyMat = new Material(Shader.Find("Standard"));
        bodyMat.color = new Color(0.1f, 0.1f, 0.12f);
        bodyMat.SetFloat("_Metallic", 0.5f);
        bodyMat.SetFloat("_Glossiness", 0.7f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMat;

        var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
        screen.name = "PhoneScreen";
        Destroy(screen.GetComponent<Collider>());
        screen.transform.SetParent(body.transform, false);
        screen.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        screen.transform.localScale = new Vector3(0.85f, 0.4f, 0.55f);
        var screenMat = new Material(Shader.Find("Sprites/Default"));
        screenMat.color = new Color(0.95f, 0.85f, 0.35f, 0.9f);   // 亮屏（自发光可见）
        screen.GetComponent<Renderer>().sharedMaterial = screenMat;
    }
}
