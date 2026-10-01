using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开局组合卡（办公楼 90 秒火场）：起火点选定后 2.5-3s 展示"本场火情：{起火点}｜{规则牌}"——
/// 每局不同的题面仪式感 + 信息公平（玩家开局就知道今晚的规则，事件保持悬念）。
/// 运行时自建 UI（深色底条+橙色标题+白色副行），unscaled 淡入淡出。
/// </summary>
public class RunIntroCard : MonoBehaviour
{
    [SerializeField] private Vector2 cardSize = new Vector2(900f, 150f);
    [SerializeField] private float fadeSeconds = 0.3f;

    private TMP_Text titleText;
    private TMP_Text subText;
    private Image backing;
    private GameObject card;
    private Coroutine running;

    private void Awake()
    {
        var canvasGo = new GameObject("RunIntroCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 58;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        card = new GameObject("Card", typeof(RectTransform));
        card.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)card.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.78f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = cardSize;

        backing = card.AddComponent<Image>();
        backing.color = new Color(0.04f, 0.04f, 0.06f, 0.86f);
        backing.raycastTarget = false;

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(card.transform, false);
        var trt = (RectTransform)titleGo.transform;
        trt.anchorMin = new Vector2(0f, 0.58f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(26f, 0f);
        trt.offsetMax = new Vector2(-26f, -8f);
        titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 34;
        titleText.color = new Color(0.96f, 0.72f, 0.32f);
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.raycastTarget = false;

        var subGo = new GameObject("Sub", typeof(RectTransform));
        subGo.transform.SetParent(card.transform, false);
        var srt = (RectTransform)subGo.transform;
        srt.anchorMin = new Vector2(0f, 0.04f);
        srt.anchorMax = new Vector2(1f, 0.56f);
        srt.offsetMin = new Vector2(26f, 6f);
        srt.offsetMax = new Vector2(-26f, 0f);
        subText = subGo.AddComponent<TextMeshProUGUI>();
        subText.fontSize = 26;
        subText.color = new Color(0.85f, 0.87f, 0.92f);
        subText.alignment = TextAlignmentOptions.Left;
        subText.raycastTarget = false;

        card.SetActive(false);
    }

    /// <summary>展示组合卡（title=第一行，sub=第二行；hold 秒后淡出）。</summary>
    public void Show(string title, string sub, float holdSeconds)
    {
        if (card == null) return;
        titleText.text = title;
        subText.text = sub;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(ShowRoutine(holdSeconds));
    }

    public void Show(string combined, float holdSeconds)
    {
        // 便捷重载：combined 形如 "第一行\n第二行"
        int idx = combined.IndexOf('\n');
        if (idx < 0) Show(combined, "", holdSeconds);
        else Show(combined.Substring(0, idx), combined.Substring(idx + 1), holdSeconds);
    }

    private IEnumerator ShowRoutine(float holdSeconds)
    {
        card.SetActive(true);
        SetAlpha(0f);

        float t = 0f;
        while (t < fadeSeconds) { t += Time.unscaledDeltaTime; SetAlpha(t / fadeSeconds); yield return null; }
        SetAlpha(1f);

        float hold = 0f;
        while (hold < holdSeconds) { hold += Time.unscaledDeltaTime; yield return null; }

        t = 0f;
        while (t < fadeSeconds) { t += Time.unscaledDeltaTime; SetAlpha(1f - t / fadeSeconds); yield return null; }
        card.SetActive(false);
        running = null;
    }

    private void SetAlpha(float a)
    {
        if (backing != null)
        {
            var c = backing.color;
            backing.color = new Color(c.r, c.g, c.b, 0.86f * a);
        }
        if (titleText != null) titleText.alpha = a;
        if (subText != null) subText.alpha = a;
    }
}
