using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 步骤弹窗：新步骤开始时屏幕中央弹出大面板（大图标+步骤名+做法说明），数秒后自动淡出；
/// 步骤完成时弹绿勾小提示。纯提示不拦截操作（全部 raycastTarget=false），动画走 unscaled 时间。
/// </summary>
public class ObjectivePopupUI : MonoBehaviour
{
    [SerializeField] private Color panelColor = new Color(0.10f, 0.11f, 0.15f, 0.92f);
    [SerializeField] private Color accentColor = new Color(0.91f, 0.38f, 0.18f, 1f);
    [SerializeField] private Color titleColor = new Color(0.95f, 0.62f, 0.35f, 1f);
    [SerializeField] private Color bodyColor = new Color(0.94f, 0.94f, 0.96f, 1f);
    [SerializeField] private Color doneColor = new Color(0.55f, 0.92f, 0.62f, 1f);
    [SerializeField] private float stepDuration = 4.5f;
    [SerializeField] private float doneDuration = 1.4f;
    [SerializeField] private float fadeIn = 0.25f;
    [SerializeField] private float fadeOut = 0.4f;

    private RectTransform stepPanel;
    private CanvasGroup stepGroup;
    private Image stepIcon;
    private TMP_Text stepTitle;
    private TMP_Text stepBody;
    private RectTransform donePanel;
    private CanvasGroup doneGroup;
    private Image doneCheck;
    private TMP_Text doneLabel;
    private Coroutine fadeRoutine;

    public static ObjectivePopupUI Create(Sprite checkIcon)
    {
        var canvasGo = new GameObject("GuidancePopupCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var go = new GameObject("ObjectivePopup", typeof(RectTransform));
        var popup = go.AddComponent<ObjectivePopupUI>();
        popup.transform.SetParent(canvasGo.transform, false);
        popup.Build(checkIcon);
        return popup;
    }

    private void Build(Sprite checkIcon)
    {
        // 步骤弹窗：大图标 + 标题 + 做法说明，屏幕中上部
        stepPanel = CreatePanel("StepPopup", panelColor, new Vector2(840f, 220f), new Vector2(0.5f, 0.5f), new Vector2(0f, 130f));
        stepGroup = stepPanel.GetComponent<CanvasGroup>();

        var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accentGo.transform.SetParent(stepPanel, false);
        var accentRt = (RectTransform)accentGo.transform;
        accentRt.anchorMin = new Vector2(0f, 0f);
        accentRt.anchorMax = new Vector2(0f, 1f);
        accentRt.pivot = new Vector2(0f, 0.5f);
        accentRt.anchoredPosition = Vector2.zero;
        accentRt.sizeDelta = new Vector2(8f, -18f);
        var accentImg = accentGo.GetComponent<Image>();
        accentImg.color = accentColor;
        accentImg.raycastTarget = false;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(stepPanel, false);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = new Vector2(88f, 0f);
        iconRt.sizeDelta = new Vector2(110f, 110f);
        stepIcon = iconGo.GetComponent<Image>();
        stepIcon.raycastTarget = false;

        var titleGo = new GameObject("Title", typeof(RectTransform));
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.fontSize = 32;
        titleTmp.color = titleColor;
        titleTmp.alignment = TextAlignmentOptions.Left;
        titleTmp.raycastTarget = false;
        var titleRt = titleTmp.rectTransform;
        titleRt.SetParent(stepPanel, false);
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0f, 1f);
        titleRt.anchoredPosition = new Vector2(165f, -30f);
        titleRt.sizeDelta = new Vector2(-200f, 44f);
        stepTitle = titleTmp;

        var bodyGo = new GameObject("Body", typeof(RectTransform));
        var bodyTmp = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyTmp.fontSize = 27;
        bodyTmp.color = bodyColor;
        bodyTmp.alignment = TextAlignmentOptions.Left;
        bodyTmp.raycastTarget = false;
        var bodyRt = bodyTmp.rectTransform;
        bodyRt.SetParent(stepPanel, false);
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0f, 0.5f);
        bodyRt.anchoredPosition = new Vector2(165f, -30f);
        bodyRt.sizeDelta = new Vector2(-200f, -84f);
        stepBody = bodyTmp;

        // 完成提示：绿勾 + 已完成
        donePanel = CreatePanel("DonePopup", panelColor, new Vector2(470f, 110f), new Vector2(0.5f, 0.5f), new Vector2(0f, 130f));
        doneGroup = donePanel.GetComponent<CanvasGroup>();

        var checkGo = new GameObject("Check", typeof(RectTransform), typeof(Image));
        checkGo.transform.SetParent(donePanel, false);
        var checkRt = (RectTransform)checkGo.transform;
        checkRt.anchorMin = new Vector2(1f, 0.5f);
        checkRt.anchorMax = new Vector2(1f, 0.5f);
        checkRt.pivot = new Vector2(0.5f, 0.5f);
        checkRt.anchoredPosition = new Vector2(-58f, 0f);
        checkRt.sizeDelta = new Vector2(52f, 52f);
        doneCheck = checkGo.GetComponent<Image>();
        doneCheck.sprite = checkIcon;
        doneCheck.raycastTarget = false;

        var doneGo = new GameObject("Label", typeof(RectTransform));
        var doneTmp = doneGo.AddComponent<TextMeshProUGUI>();
        doneTmp.fontSize = 30;
        doneTmp.color = doneColor;
        doneTmp.alignment = TextAlignmentOptions.Left;
        doneTmp.raycastTarget = false;
        var doneRt = doneTmp.rectTransform;
        doneRt.SetParent(donePanel, false);
        doneRt.anchorMin = new Vector2(0f, 0f);
        doneRt.anchorMax = new Vector2(1f, 1f);
        doneRt.pivot = new Vector2(0f, 0.5f);
        doneRt.anchoredPosition = new Vector2(30f, 0f);
        doneRt.sizeDelta = new Vector2(-120f, 0f);   // 右侧留出绿勾位置，避免文字压勾
        doneLabel = doneTmp;

        stepPanel.gameObject.SetActive(false);
        donePanel.gameObject.SetActive(false);
    }

    private RectTransform CreatePanel(string name, Color color, Vector2 size, Vector2 anchor, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        go.GetComponent<CanvasGroup>().blocksRaycasts = false;
        return rt;
    }

    /// <summary>弹出步骤说明（自动淡出）。</summary>
    public void ShowStep(Sprite icon, string title, string instruction)
    {
        donePanel.gameObject.SetActive(false);
        stepPanel.gameObject.SetActive(true);
        stepIcon.sprite = icon;
        stepTitle.text = title;
        stepBody.text = instruction;
        StartFade(stepGroup, stepDuration);
    }

    /// <summary>弹出完成提示。</summary>
    public void ShowDone(Sprite checkIcon, string label)
    {
        stepPanel.gameObject.SetActive(false);
        donePanel.gameObject.SetActive(true);
        doneCheck.sprite = checkIcon;
        doneLabel.text = "已完成 · " + label;
        StartFade(doneGroup, doneDuration);
    }

    public void HideNow()
    {
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
        stepPanel.gameObject.SetActive(false);
        donePanel.gameObject.SetActive(false);
    }

    private void StartFade(CanvasGroup g, float total)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(g, total));
    }

    private IEnumerator FadeRoutine(CanvasGroup g, float total)
    {
        float t = 0f;
        g.alpha = 0f;
        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Clamp01(t / fadeIn);
            yield return null;
        }
        g.alpha = 1f;
        float hold = Mathf.Max(0.1f, total - fadeIn - fadeOut);
        yield return new WaitForSecondsRealtime(hold);
        t = 0f;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = 1f - Mathf.Clamp01(t / fadeOut);
            yield return null;
        }
        g.alpha = 0f;
        g.gameObject.SetActive(false);
        fadeRoutine = null;
    }
}
