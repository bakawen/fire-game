using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 天花板烟感红光（办公楼"环境语言"系统）：把场景里现成的 41 个烟感探测器变成火势地图。
/// 探测器按所在区归组，下沿小红光随区烟度分级：<0.15 熄；0.15~0.4 琥珀缓闪（烟已入区）；≥0.4 红色急闪（浓烟封区）。
/// 玩家抬头扫一眼天花板，就能读出"哪几个区正在恶化"。探测器是烟感元件——天然不受报警联动限制。
/// </summary>
public class SmokeDetectorSystem : MonoBehaviour
{
    [SerializeField] private FireZoneGraph graph;
    [Tooltip("探测器警报光尺寸（米）——要一眼可辨")]
    [SerializeField] private float lampSize = 0.30f;
    [SerializeField] private float amberBlink = 1.6f;   // 琥珀缓闪频率
    [SerializeField] private float redBlink = 4.5f;     // 红色急闪频率

    private class Lamp
    {
        public Renderer rend;
        public int zone;
    }

    private readonly List<Lamp> lamps = new List<Lamp>();
    private MaterialPropertyBlock mpb;
    private static Material lampMat;

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();
        if (lampMat == null)
        {
            lampMat = new Material(Shader.Find("Sprites/Default"));
            lampMat.mainTexture = SoftGlowTexture();   // 径向柔光，硬边方块会像个悬浮贴片
        }
        if (graph == null) return;
        foreach (var go in FindObjectsOfType<GameObject>(true))
        {
            if (!go.name.Contains("SmokeDetector")) continue;
            int zone = graph.GetZoneIndexAt(go.transform.position);
            if (zone < 0) continue;   // 场外/区外的探测器不接管

            var lampGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lampGo.name = "DetectorGlow";
            Destroy(lampGo.GetComponent<Collider>());
            lampGo.transform.SetParent(go.transform, false);
            lampGo.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            lampGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 发光面朝下
            lampGo.transform.localScale = Vector3.one * lampSize;
            var r = lampGo.GetComponent<Renderer>();
            r.sharedMaterial = lampMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            lamps.Add(new Lamp { rend = r, zone = zone });
        }
    }

    private static Texture2D SoftGlowTexture()
    {
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(S - 1f, S - 1f)) * 2f / (S - 1f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a;   // 径向平方衰减，边缘柔和
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }

    private void Update()
    {
        if (graph == null) return;
        float t = Time.time;
        foreach (var l in lamps)
        {
            float smoke = graph.GetZoneSmoke(l.zone);
            bool on = smoke >= 0.15f;
            l.rend.enabled = on;
            if (!on) continue;

            Color c;
            if (smoke < 0.4f)
                c = new Color(1f, 0.62f, 0.15f, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * amberBlink)));
            else
                c = new Color(1f, 0.12f, 0.10f, 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(t * redBlink)));
            mpb.SetColor("_Color", c);
            l.rend.SetPropertyBlock(mpb);
        }
    }
}
