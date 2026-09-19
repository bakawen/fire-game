using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 地面路径导航（疏散箭头链版）：NavMesh.CalculatePath 从玩家到当前步骤目标，
/// 沿路径每隔 spacing 平贴一枚绿色发光疏散箭头（朝向前进方向，亮度波沿路径向目标流动，模拟疏散灯逐级点亮），
/// 玩家移动时每 0.25s 重算。仅由宿舍关的 GuidanceChecklist 创建，其他关卡不受影响。
/// </summary>
public class PathGuide : MonoBehaviour
{
    [SerializeField] private float refreshInterval = 0.25f;
    [SerializeField] private float spacing = 0.8f;
    [SerializeField] private float arrowSize = 0.62f;
    [SerializeField] private float arrowY = 0.03f;
    [SerializeField] private int poolSize = 72;

    [Header("流光动画")]
    [SerializeField] private float flowSpeed = 0.9f;       // 波峰每秒推进的波长数
    [SerializeField] private float waveLength = 3.2f;      // 明暗波的空间长度（米）
    [SerializeField] private float brightnessMin = 0.38f;  // 最暗亮度，保证箭头始终可读

    private Transform player;
    private Transform target;
    private float nextRefresh;
    private static readonly NavMeshPath path = new NavMeshPath();
    private GameObject[] arrows;
    private Renderer[] arrowRenderers;
    private float[] arrowDist;
    private MaterialPropertyBlock mpb;
    private float flowPhase;
    private int lastUsed;

    public static PathGuide Create(Transform playerTransform, Texture2D arrowTexture = null)
    {
        var go = new GameObject("PathGuide");
        var guide = go.AddComponent<PathGuide>();
        guide.player = playerTransform;
        guide.BuildPool(arrowTexture);
        guide.Hide();
        return guide;
    }

    private void BuildPool(Texture2D arrowTexture)
    {
        var tex = arrowTexture != null ? arrowTexture : ProceduralArrowTexture();
        // Sprites/Default 自带 _Color 且无光照，MPB 可逐箭头调色（Unlit/Transparent 没有 _Color，color 会被静默忽略）
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = tex;
        mat.color = new Color(0.8f, 1f, 0.85f, 0.92f);   // 绿色 tint 兜底
        arrows = new GameObject[poolSize];
        arrowRenderers = new Renderer[poolSize];
        arrowDist = new float[poolSize];
        mpb = new MaterialPropertyBlock();

        var container = new GameObject("GroundArrows");
        container.transform.SetParent(transform, false);
        for (int i = 0; i < poolSize; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Arrow" + i;
            q.transform.SetParent(container.transform, false);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            var mr = q.GetComponent<MeshRenderer>();
            arrowRenderers[i] = mr;
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            q.SetActive(false);
            arrows[i] = q;
        }
    }

    private static Texture2D ProceduralArrowTexture()
    {
        var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                float u = x / 127f, v = y / 127f;
                // 粗V形箭头（指向+v），白色+alpha 通道由 _Color 乘绿色 tint
                bool inShaft = u > 0.36f && u < 0.64f && v > 0.18f && v < 0.85f;
                bool inHead = v <= 0.42f && v > 0.1f && Mathf.Abs(u - 0.5f) < (0.42f - v) * 1.05f;
                float a = (inShaft || inHead) ? 1f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    public void SetTarget(Transform t)
    {
        target = t;
        if (target == null) { Hide(); return; }
        nextRefresh = 0f;
        Rebuild();
    }

    public void Hide()
    {
        target = null;
        lastUsed = 0;
        if (arrows == null) return;
        foreach (var a in arrows) if (a != null) a.SetActive(false);
    }

    private void Update()
    {
        if (target == null || player == null) return;

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + refreshInterval;
            Rebuild();
        }

        // 疏散灯式流光：亮度波峰随时间沿路径向目标推进（相位按沿路距离偏移）
        flowPhase += Time.unscaledDeltaTime * flowSpeed;
        for (int i = 0; i < lastUsed; i++)
        {
            if (arrows[i] == null || arrowRenderers[i] == null) continue;
            float wave = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (flowPhase - arrowDist[i] / waveLength));
            float b = Mathf.Lerp(brightnessMin, 1f, wave);
            mpb.SetColor("_Color", new Color(0.8f * b, 1f * b, 0.85f * b, 0.92f));
            arrowRenderers[i].SetPropertyBlock(mpb);
        }
    }

    private void Rebuild()
    {
        if (!NavMesh.CalculatePath(player.position, target.position, NavMesh.AllAreas, path) ||
            path.status != NavMeshPathStatus.PathComplete)
        {
            lastUsed = 0;
            foreach (var a in arrows) if (a != null) a.SetActive(false);
            return;
        }

        var corners = path.corners;
        int used = 0;
        float acc = spacing * 0.5f;   // 第一枚箭头在玩家前方半个间距
        float traveled = 0f;          // 起点到当前段的沿路距离，决定流光相位
        for (int seg = 0; seg < corners.Length - 1 && used < arrows.Length; seg++)
        {
            Vector3 a = corners[seg], b = corners[seg + 1];
            float segLen = Vector3.Distance(a, b);
            if (segLen < 0.01f) continue;
            Vector3 dir = (b - a).normalized;
            while (acc <= segLen && used < arrows.Length)
            {
                Vector3 pos = a + (b - a) * (acc / segLen);
                var arrow = arrows[used];
                arrow.SetActive(true);
                arrow.transform.position = pos + Vector3.up * arrowY;
                arrow.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z).normalized) * Quaternion.Euler(90f, 0f, 0f);
                arrow.transform.localScale = Vector3.one * arrowSize;
                arrowDist[used] = traveled + acc;
                used++;
                acc += spacing;
            }
            acc -= segLen;
            traveled += segLen;
        }
        for (int i = used; i < arrows.Length; i++)
            if (arrows[i] != null) arrows[i].SetActive(false);
        lastUsed = used;
    }
}
