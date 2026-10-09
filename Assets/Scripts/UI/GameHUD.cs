using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>游戏内 HUD：任务目标、用时计时、交互提示、长按进度圈、受伤红晕、灭火器剂量。返回入口由 Esc 暂停面板承担。</summary>
public class GameHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text timerText;

    [Header("M3 扩展")]
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Image vignetteImage;
    [SerializeField] private GameObject doseRoot;
    [SerializeField] private Image doseFillImage;
    [SerializeField] private PlayerInteraction player;

    private float elapsed;   // 缩放时间累加：Esc 暂停（timeScale=0）时计时冻结，结算用时口径一致

    private GameObject holdRingCanvas;
    private Image holdRingFill;
    private static Sprite _ringSprite;

    public float ElapsedSeconds => elapsed;

    private void Awake()
    {
        BuildHoldRing();
    }

    private void Update()
    {
        if (Time.timeScale > 0.01f) elapsed += Time.deltaTime;
        int total = (int)elapsed;
        timerText.text = $"{total / 60:00}:{total % 60:00}";

        if (player != null)
        {
            string prompt = player.TargetPrompt;
            if (!string.IsNullOrEmpty(prompt))
            {
                promptText.text = prompt;
                promptText.gameObject.SetActive(true);
            }
            else
            {
                promptText.gameObject.SetActive(false);
            }

            FireExtinguisher extinguisher = player.HeldItem as FireExtinguisher;
            if (extinguisher != null)
            {
                doseRoot.SetActive(true);
                doseFillImage.fillAmount = extinguisher.Dose;
            }
            else
            {
                doseRoot.SetActive(false);
            }
        }

        // 长按进度圈（固守待援等按住型交互）：0~1 显示，-1 隐藏
        float hp = player != null ? player.HoldProgress : -1f;
        if (holdRingCanvas != null)
        {
            bool show = hp >= 0f;
            if (holdRingCanvas.activeSelf != show) holdRingCanvas.SetActive(show);
            if (show && holdRingFill != null) holdRingFill.fillAmount = Mathf.Clamp01(hp);
        }
    }

    /// <summary>准星长按进度圈：运行时自建（零场景改动），径向填充跟随 HoldProgress。</summary>
    private void BuildHoldRing()
    {
        var canvasGo = new GameObject("HoldRingCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;   // 高于默认HUD层，低于暂停面板(800)与淡出层(32767)
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var ringSprite = RingSprite();

        // 底环（暗色常显底）
        var bgGo = new GameObject("RingBg", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgRt = (RectTransform)bgGo.transform;
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = new Vector2(0f, 40f);   // 准星正上方
        bgRt.sizeDelta = new Vector2(56f, 56f);
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.sprite = ringSprite;
        bgImg.type = Image.Type.Filled;
        bgImg.fillMethod = Image.FillMethod.Radial360;
        bgImg.fillOrigin = 2;   // Top
        bgImg.fillAmount = 1f;
        bgImg.color = new Color(0f, 0f, 0f, 0.4f);
        bgImg.raycastTarget = false;

        // 进度环（橙金，随按住时长径向填充）
        var fgGo = new GameObject("RingFill", typeof(RectTransform), typeof(Image));
        fgGo.transform.SetParent(canvasGo.transform, false);
        var fgRt = (RectTransform)fgGo.transform;
        fgRt.anchorMin = fgRt.anchorMax = fgRt.pivot = new Vector2(0.5f, 0.5f);
        fgRt.anchoredPosition = bgRt.anchoredPosition;
        fgRt.sizeDelta = bgRt.sizeDelta;
        holdRingFill = fgGo.GetComponent<Image>();
        holdRingFill.sprite = ringSprite;
        holdRingFill.type = Image.Type.Filled;
        holdRingFill.fillMethod = Image.FillMethod.Radial360;
        holdRingFill.fillOrigin = 2;
        holdRingFill.fillClockwise = true;
        holdRingFill.fillAmount = 0f;
        holdRingFill.color = new Color(0.96f, 0.62f, 0.18f, 0.95f);   // 橙金（与交互主色一致）
        holdRingFill.raycastTarget = false;

        canvasGo.SetActive(false);
        holdRingCanvas = canvasGo;
    }

    /// <summary>环形贴图（白色圆环，程序生成一次缓存）。</summary>
    private static Sprite RingSprite()
    {
        if (_ringSprite != null) return _ringSprite;
        int sz = 96;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        float c = (sz - 1) / 2f;
        float rOuter = sz / 2f - 3f;
        float rInner = rOuter - 8f;
        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                // 环带 + 0.5px 软边抗锯齿
                float a = Mathf.Clamp01(Mathf.Min(d - rInner, rOuter - d) + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), 100);
        return _ringSprite;
    }

    public void SetObjective(string text)
    {
        objectiveText.text = text;
    }

    /// <summary>图标化引导接管时隐藏静态目标文字行（GuidanceChecklist 用）。</summary>
    public void SetObjectiveVisible(bool visible)
    {
        if (objectiveText != null) objectiveText.gameObject.SetActive(visible);
    }

    /// <summary>受伤红晕（0=无伤，1=濒死）。</summary>
    public void SetVignette(float alpha)
    {
        if (vignetteImage == null) return;
        Color c = vignetteImage.color;
        c.a = Mathf.Clamp01(alpha);
        vignetteImage.color = c;
    }
}
