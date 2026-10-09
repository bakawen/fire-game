using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 浮动标记的文字标签：ScreenSpaceOverlay TMP 每帧跟随标记世界位置（WorldToScreenPoint）。
/// 相比世界空间 3D TMP：字号像素级锐利、不受场景光照/雾/相机俯仰影响（UGUI 管线全项目验证）。
/// 与 InteractableMarker 同挂一个 GO；画布挂场景根级（勿挂标记下——公告板每帧旋转+浮动会干扰画布驱动器）。
/// </summary>
public class MarkerLabelUI : MonoBehaviour
{
    [SerializeField] private string labelText = "主入口";
    [SerializeField] private float offsetUp = 0.3f;      // 高于三角图标的世界偏移
    [SerializeField] private float yOffPx = 40f;         // 屏幕坐标下的垂直偏移（参考分辨率）
    [SerializeField] private float visibleRange = 30f;   // 与 InteractableMarker 一致

    private GameObject canvasGO;
    private RectTransform canvasRect;
    private Canvas canvas;
    private TMP_Text label;
    private Camera cam;
    private Transform player;

    private void Awake()
    {
        // 画布挂根级：跟随由 WorldToScreenPoint 完成，不需要父子关系
        canvasGO = new GameObject("MarkerLabelCanvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;   // 于 HUD 之上、暂停面板之下
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasRect = canvasGO.GetComponent<RectTransform>();

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(canvasGO.transform, false);
        label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = labelText;   // 默认字体经 TMP Settings fallback 渲染中文（NotoSansSC）
        label.fontSize = 34;
        label.color = new Color(0.5f, 1f, 0.6f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.fontStyle = FontStyles.Bold;

        // 描边（暗背景/亮背景均可读，全项目 PauseUI/GameHUD 同款）
        var m = label.fontMaterial;
        m.EnableKeyword("OUTLINE_ON");
        m.SetFloat(Shader.PropertyToID("_OutlineWidth"), 0.2f);
        m.SetColor(Shader.PropertyToID("_OutlineColor"), Color.black);
    }

    private void OnDestroy()
    {
        if (canvasGO != null) Destroy(canvasGO);
    }

    /// <summary>标记完成隐藏（InteractableMeter SetActive(false) 触发本组件停用）时同步隐藏标签。</summary>
    private void OnDisable()
    {
        if (canvasGO != null) canvasGO.SetActive(false);
    }

    private void OnEnable()
    {
        if (canvasGO != null) canvasGO.SetActive(true);
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (player == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) player = p.transform;
        }
        if (cam == null || player == null || canvasGO == null) return;

        float d = Vector3.Distance(player.position, transform.position);
        var sp = cam.WorldToScreenPoint(transform.position + Vector3.up * offsetUp);
        bool onScreen = sp.z > 0f && sp.x > 0f && sp.x < Screen.width && sp.y > 0f && sp.y < Screen.height;
        bool show = d <= visibleRange && onScreen;
        if (canvasGO.activeSelf != show) canvasGO.SetActive(show);
        if (!show) return;

        // Screen(px) → Canvas 局部（参考分辨率 1920×1080）；ScreenSpaceCamera 模式必须传它的 worldCamera
        var convCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, convCam, out var local);
        label.rectTransform.localPosition = local + new Vector2(0f, yOffPx);
    }
}
