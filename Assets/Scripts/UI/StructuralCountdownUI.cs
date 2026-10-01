using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 结构失效倒计时 HUD（办公楼终局死线）：顶部中央红字脉冲"结构失效 mm:ss"。
/// 运行时自建 Canvas（仿 RescueCountdown 管线），由 StructuralFailure 驱动 Begin/SetRemaining/Stop。
/// </summary>
public class StructuralCountdownUI : MonoBehaviour
{
    [SerializeField] private Vector2 labelSize = new Vector2(760f, 54f);

    private TMP_Text label;
    private bool visible;

    private void Awake()
    {
        var canvasGo = new GameObject("StructuralCountdownCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 56;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var go = new GameObject("StructuralCountdownLabel", typeof(RectTransform));
        go.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -14f);
        rt.sizeDelta = labelSize;
        label = go.AddComponent<TextMeshProUGUI>();
        label.fontSize = 34;
        label.color = new Color(1f, 0.28f, 0.2f, 0.95f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.text = "";
    }

    public void Begin()
    {
        visible = true;
        if (label != null) label.text = "";
    }

    public void SetRemaining(float seconds)
    {
        if (!visible || label == null) return;
        int total = Mathf.Max(0, (int)seconds);
        float pulse = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
        label.text = $"<alpha=#{(int)(pulse * 255):X2}>⚠ 结构失效  {total / 60:00}:{total % 60:00}";
    }

    public void Stop()
    {
        visible = false;
        if (label != null) label.text = "";
    }
}
