using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 出口牌活语言（办公楼"环境语言"系统）：报警联动——没按警报前整栋楼是哑的（牌子只是静态装饰）；
/// 报警后，每块出口牌用下沿状态灯报告它守望的方向，三态语言：
/// 绿色常亮=此路可走；琥珀缓闪=此路正在恶化（快！）；红色急闪=此路已被浓烟侵蚀（换路！）。
/// 岔口的双牌对比就是"读火场选路线"的操作化；唯一出口牌则用琥珀给出"仍在但告急"的紧迫梯度。
/// 状态灯是运行时挂在牌子下的发光条（Sprites/Default 无光照、MPB 逐实例调色，静态烘焙几何可见）。
/// </summary>
public class ExitSignSystem : MonoBehaviour
{
    public enum SignState { Green, Amber, Red }

    [System.Serializable]
    public class Sign
    {
        [Tooltip("出口牌对象（状态灯挂在它下面）")]
        public Transform signRoot;
        [Tooltip("这块牌守望的区域下标——进这个区域的路")]
        public int zoneIndex;
        [Tooltip("区域烟度超过此值→琥珀告急")]
        public float smokeThreshold = 0.30f;
        [Tooltip("区域烟度超过此值→红色封路")]
        public float criticalThreshold = 0.55f;
        [NonSerialized] public Renderer lamp;
    }

    [Header("引用")]
    [SerializeField] private List<Sign> signs = new List<Sign>();
    [SerializeField] private FireZoneGraph graph;
    [SerializeField] private AlarmButton alarmButton;

    [Header("状态灯外观")]
    [SerializeField] private Vector2 lampSize = new Vector2(0.72f, 0.13f);
    [SerializeField] private Vector3 lampOffset = new Vector3(0f, -0.55f, 0.02f);
    [SerializeField] private float blinkSpeed = 3.4f;
    [SerializeField] private Color passColor = new Color(0.35f, 1f, 0.5f, 0.85f);
    [SerializeField] private Color amberColor = new Color(1f, 0.62f, 0.15f, 1f);
    [SerializeField] private Color blockedColor = new Color(1f, 0.16f, 0.12f, 1f);

    private MaterialPropertyBlock mpb;
    private static Material lampMat;

    /// <summary>是否已报警（联动上线）。</summary>
    public bool Armed => alarmButton == null || !alarmButton.CanInteract;

    /// <summary>查询某块牌的三态（断言/测试用）。</summary>
    public SignState GetState(int signIndex)
    {
        if (signs == null || signIndex < 0 || signIndex >= signs.Count || graph == null) return SignState.Green;
        var s = signs[signIndex];
        if (s == null) return SignState.Green;
        float smoke = graph.GetZoneSmoke(s.zoneIndex);
        if (smoke >= s.criticalThreshold) return SignState.Red;
        if (smoke >= s.smokeThreshold) return SignState.Amber;
        return SignState.Green;
    }

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();
        if (lampMat == null)
        {
            lampMat = new Material(Shader.Find("Sprites/Default"));
            lampMat.color = Color.white;
        }
        foreach (var s in signs)
        {
            if (s == null || s.signRoot == null || s.lamp != null) continue;
            var lampGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lampGo.name = "SignStatusLamp";
            Destroy(lampGo.GetComponent<Collider>());
            lampGo.transform.SetParent(s.signRoot, false);
            lampGo.transform.localPosition = lampOffset;
            lampGo.transform.localRotation = Quaternion.identity;
            lampGo.transform.localScale = new Vector3(lampSize.x, lampSize.y, 1f);
            var r = lampGo.GetComponent<Renderer>();
            r.sharedMaterial = lampMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            s.lamp = r;
        }
    }

    private void Update()
    {
        bool armed = Armed;
        float t = Time.time;
        for (int i = 0; i < signs.Count; i++)
        {
            var s = signs[i];
            if (s == null || s.lamp == null) continue;
            if (!armed)
            {
                // 未报警：楼是哑的——状态灯全部熄灭
                if (s.lamp.enabled) s.lamp.enabled = false;
                continue;
            }
            s.lamp.enabled = true;
            float smoke = graph != null ? graph.GetZoneSmoke(s.zoneIndex) : 0f;
            Color c;
            if (smoke >= s.criticalThreshold)
            {
                // 红色急闪：此路已被浓烟侵蚀
                c = new Color(blockedColor.r, blockedColor.g, blockedColor.b,
                    0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * blinkSpeed)));
            }
            else if (smoke >= s.smokeThreshold)
            {
                // 琥珀缓闪：此路正在恶化——快！
                c = new Color(amberColor.r, amberColor.g, amberColor.b,
                    0.45f + 0.55f * Mathf.Abs(Mathf.Sin(t * blinkSpeed * 0.45f)));
            }
            else
            {
                c = passColor;   // 绿色常亮：此路可走
            }
            mpb.SetColor("_Color", c);
            s.lamp.SetPropertyBlock(mpb);
        }
    }
}
