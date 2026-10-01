using System.Collections;
using UnityEngine;

/// <summary>
/// 防火卷帘（办公楼设施联动）：报警按钮按下后延迟 descendDelay 秒开始下降，descentSeconds 秒落定。
/// 落定=IsDown=true（FireZoneGraph 链阀门关闭：挡火挡烟）；卷帘旁的疏散边门仍是 InteractableDoor——
/// 玩家可以走边门，但开门=烟流通路重开（门=阀门教学再深化：联动保护是暂时的，敞开的门是烟的入口）。
/// 视觉=运行时程序化（瓦楞金属面板+顶部卷帘箱+底部自发光状态条：下降中琥珀脉冲/落定红）——动态物件，
/// 不参与光照烘焙。规则牌"卷帘检修"（ShutterMaintenance）→ LinkageDisabled=true，报警不联动。
/// </summary>
public class FireShutter : MonoBehaviour
{
    [Header("参数")]
    [Tooltip("开口净宽（米）")]
    [SerializeField] private float openingWidth = 3.0f;
    [Tooltip("开口净高（米）")]
    [SerializeField] private float openingHeight = 2.6f;
    [SerializeField] private float descendDelaySeconds = 8f;
    [SerializeField] private float descendSeconds = 8f;
    [Tooltip("警告提前量：广播先于下降开始的秒数")]
    [SerializeField] private float warnLeadSeconds = 4f;
    [Tooltip("本卷帘的通行位置名（广播/记账文案用）")]
    [SerializeField] private string locationLabel = "防火卷帘";

    [Header("联动门与替代路线")]
    [Tooltip("本卷帘封住的门（落下后锁其交互——门在卷帘后面开了也走不过去，明确封死更清晰）")]
    [SerializeField] private InteractableDoor linkedDoor;
    [Tooltip("替代疏散门标记（卷帘落下后在它上方亮绿色引导灯+文字）")]
    [SerializeField] private Transform alternateMark;

    [Header("引用")]
    [SerializeField] private AlarmButton alarmButton;
    [SerializeField] private AudioSource audioSrc;
    [SerializeField] private AudioClip klaxonClip;     // 下降警报（可复用现有警笛）
    [SerializeField] private AudioClip motorClip;       // 电机运行循环
    [SerializeField] private AudioClip landedClip;      // 落定闷响
    [SerializeField] private TutorialUI tutorial;

    [Header("广播文案")]
    [SerializeField, TextArea(1, 2)] private string warningBroadcast = "消防广播：防火卷帘即将下降分区隔烟——请迅速通过，或改走疏散门！";
    [SerializeField, TextArea(1, 3)] private string landedHint = "防火卷帘已落下，此路封闭——请改走疏散门！";

    /// <summary>规则牌联动：卷帘检修中（本局报警不联动）。</summary>
    public static bool LinkageDisabled;

    /// <summary>落定=true（FireZoneGraph 每 tick 查询阀门状态）。</summary>
    public bool IsDown { get; private set; }

    public string LocationLabel => locationLabel;

    private enum State { Raised, Warned, Descending, Down }
    private State state = State.Raised;

    private GameObject panel;
    private GameObject housing;
    private GameObject strip;
    private Renderer stripRend;
    private Renderer glowRend;      // 替代疏散门引导灯（落定后生成）
    private BoxCollider col;
    private Vector3 raisedPos;     // 面板完全收起时的局部位置（藏进卷帘箱）
    private Vector3 downPos;       // 面板落定的局部位置（上沿对齐锚点）
    private MaterialPropertyBlock mpb;
    private static Material panelMat;
    private static Material housingMat;
    private static Material stripMat;

    private void Awake()
    {
        if (alarmButton == null) alarmButton = FindFirstObjectByType<AlarmButton>();
        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialUI>();
        // 卷帘落下后锁住被封门的交互：门在卷帘后面开了也走不过去——明确封死比"幽灵开门"清晰
        if (linkedDoor != null) linkedDoor.InteractGate = () => !IsDown;
        if (audioSrc == null)
        {
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 1f;
            audioSrc.rolloffMode = AudioRolloffMode.Linear;
            audioSrc.minDistance = 3f;
            audioSrc.maxDistance = 26f;
        }

        BuildVisuals();
        mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (alarmButton != null) alarmButton.OnAlarmTriggered += OnAlarm;
    }

    private void OnDisable()
    {
        if (alarmButton != null) alarmButton.OnAlarmTriggered -= OnAlarm;
    }

    private void OnAlarm()
    {
        if (state != State.Raised) return;
        if (LinkageDisabled) return;   // 规则牌：卷帘检修中——报警不联动
        StartCoroutine(AlarmSequenceRoutine());
    }

    /// <summary>报警后时序：延迟(descendDelay-warnLead)发警告广播 → warnLead 后开始下降 → 落定。</summary>
    private IEnumerator AlarmSequenceRoutine()
    {
        // 警告提前广播——多道卷帘以不同 delay 错峰，避免广播在同一帧互相覆盖
        float warnWait = Mathf.Max(0f, descendDelaySeconds - warnLeadSeconds);
        if (warnWait > 0f) yield return new WaitForSeconds(warnWait);

        state = State.Warned;
        if (klaxonClip != null) audioSrc.PlayOneShot(klaxonClip, 0.9f);
        if (tutorial != null) tutorial.ShowHint(warningBroadcast);
        DecisionLedger.RecordShutterArmed(locationLabel);

        if (warnLeadSeconds > 0f) yield return new WaitForSeconds(warnLeadSeconds);
        yield return DescendRoutine();
    }

    private IEnumerator DescendRoutine()
    {
        state = State.Descending;
        panel.SetActive(true);
        if (motorClip != null)
        {
            audioSrc.clip = motorClip;
            audioSrc.loop = true;
            audioSrc.Play();
        }

        float t = 0f;
        while (t < descendSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / descendSeconds);
            panel.transform.localPosition = Vector3.Lerp(raisedPos, downPos, k);
            // 面板下沿过人腰高度后碰撞生效（下降中的卷帘不能再钻）
            if (col != null && !col.enabled && k > 0.35f) col.enabled = true;
            yield return null;
        }

        panel.transform.localPosition = downPos;
        state = State.Down;
        IsDown = true;
        audioSrc.Stop();
        audioSrc.loop = false;
        if (landedClip != null) audioSrc.PlayOneShot(landedClip, 1f);
        DecisionLedger.RecordShutterSealed(locationLabel);
        if (tutorial != null) tutorial.ShowHint(landedHint);
        SpawnAlternateGlow();
    }

    private void Update()
    {
        if (state == State.Descending && stripRend != null)
        {
            // 下降中：琥珀脉冲
            float p = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
            mpb.SetColor("_Color", new Color(1f, 0.62f, 0.15f, p));
            stripRend.SetPropertyBlock(mpb);
        }
        else if (state == State.Down)
        {
            if (stripRend != null)
            {
                mpb.SetColor("_Color", new Color(1f, 0.16f, 0.12f, 0.9f));
                stripRend.SetPropertyBlock(mpb);
            }
            // 替代疏散门引导灯：绿色脉冲
            if (glowRend != null)
            {
                float g = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 2.6f));
                mpb.SetColor("_Color", new Color(0.35f, 1f, 0.5f, g));
                glowRend.SetPropertyBlock(mpb);
            }
        }
    }

    /// <summary>卷帘落定后：在替代疏散门上方生成绿色引导灯+文字（"此路封了，走那扇"的环境语言）。</summary>
    private void SpawnAlternateGlow()
    {
        if (alternateMark == null) return;
        var root = new GameObject("AlternateDoorGlow");
        root.transform.position = alternateMark.position;
        root.transform.rotation = alternateMark.rotation;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Glow";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(root.transform, false);
        quad.transform.localScale = new Vector3(1.15f, 0.24f, 1f);
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(0.35f, 1f, 0.5f, 0.9f);
        var r = quad.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        glowRend = r;

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(root.transform, false);
        textGo.transform.localPosition = new Vector3(0f, -0.24f, -0.02f);
        textGo.transform.localEulerAngles = new Vector3(0f, 180f, 0f);
        var tm = textGo.AddComponent<TextMesh>();
        tm.text = "疏散门";
        tm.fontSize = 46;
        tm.characterSize = 0.07f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.4f, 1f, 0.55f);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.font = font;
        textGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    /// <summary>程序化视觉：卷帘箱（常显）+ 金属面板（初始收起）+ 底部状态条。</summary>
    private void BuildVisuals()
    {
        if (panelMat == null)
        {
            panelMat = new Material(Shader.Find("Standard"));
            panelMat.color = new Color(0.66f, 0.67f, 0.70f);
            panelMat.SetFloat("_Metallic", 0.75f);
            panelMat.SetFloat("_Glossiness", 0.35f);
        }
        if (housingMat == null)
        {
            housingMat = new Material(Shader.Find("Standard"));
            housingMat.color = new Color(0.35f, 0.36f, 0.39f);
            housingMat.SetFloat("_Metallic", 0.6f);
            housingMat.SetFloat("_Glossiness", 0.3f);
        }
        if (stripMat == null)
        {
            stripMat = new Material(Shader.Find("Sprites/Default"));   // 自发光可见性（静态场景不吃实时光）
            stripMat.color = new Color(1f, 0.62f, 0.15f, 0.5f);
        }

        // 顶部卷帘箱（常显，暗示这里有一道卷帘）
        housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        housing.name = "ShutterHousing";
        Destroy(housing.GetComponent<Collider>());
        housing.transform.SetParent(transform, false);
        housing.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        housing.transform.localScale = new Vector3(openingWidth + 0.4f, 0.42f, 0.22f);
        housing.GetComponent<Renderer>().sharedMaterial = housingMat;

        // 面板（上沿为锚点向下延伸；收起时整体藏进箱体）
        panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "ShutterPanel";
        panel.transform.SetParent(transform, false);
        panel.transform.localScale = new Vector3(openingWidth, openingHeight, 0.08f);
        var pr = panel.GetComponent<Renderer>();
        pr.sharedMaterial = panelMat;
        // 瓦楞感：窄向深色条纹叠一层薄盒（避免多材质数组，用 5 条细棱做浮凸感）
        for (int i = 1; i < 5; i++)
        {
            var rib = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rib.name = "Rib";
            Destroy(rib.GetComponent<Collider>());
            rib.transform.SetParent(panel.transform, false);
            float y = -openingHeight * 0.5f + openingHeight * i / 5f;
            rib.transform.localPosition = new Vector3(0f, y, 0.05f);
            rib.transform.localScale = new Vector3(openingWidth * 0.96f, 0.06f, 0.02f);
            rib.GetComponent<Renderer>().sharedMaterial = housingMat;
        }
        col = panel.GetComponent<BoxCollider>();
        col.enabled = false;

        raisedPos = new Vector3(0f, openingHeight * 0.5f + 0.3f, 0f);   // 收进箱体
        downPos = new Vector3(0f, -openingHeight * 0.5f, 0f);            // 上沿对齐锚点
        panel.transform.localPosition = raisedPos;
        panel.SetActive(false);   // 报警前只看到卷帘箱，面板藏在里面

        // 底部状态条（贴面板下沿）
        strip = GameObject.CreatePrimitive(PrimitiveType.Quad);
        strip.name = "ShutterStrip";
        Destroy(strip.GetComponent<Collider>());
        strip.transform.SetParent(panel.transform, false);
        strip.transform.localPosition = new Vector3(0f, -openingHeight * 0.5f + 0.05f, 0.06f);
        strip.transform.localScale = new Vector3(openingWidth * 0.9f, 0.1f, 1f);
        stripRend = strip.GetComponent<Renderer>();
        stripRend.sharedMaterial = stripMat;
        stripRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        stripRend.receiveShadows = false;
    }
}
