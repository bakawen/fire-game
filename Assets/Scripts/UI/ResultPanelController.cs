using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 胜负结算面板（代码自建，2026-10 重构）：全屏遮罩+居中卡片+标题+可滚动详情+底部按钮行。
/// 旧场景序列化面板（panelRoot，700×170 定尺寸）内容超长必溢出，Awake 中自动退役；
/// 公共 API（ShowWin/ShowLose/Hide）不变——WinTrigger/119Hooks 等调用点零改动。
/// </summary>
public class ResultPanelController : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;   // 旧场景面板：Awake 中退役
    [SerializeField] private TMP_Text titleText;     // 旧引用：Awake 中重指到新面板
    [SerializeField] private TMP_Text detailText;    // 旧引用：同上
    [SerializeField] private Button retryButton;     // 旧引用：同上
    [SerializeField] private Button exitButton;      // 旧引用：同上

    private GameObject newRoot;
    private TMP_Text newTitle;
    private TMP_Text newDetail;
    private RectTransform scrollContent;
    private ScrollRect scroll;
    private GameHUD hud;

    private void Awake()
    {
        // 旧场景面板退役（避免与新面板叠加；组件本身保留以维持场景引用）
        if (panelRoot != null) panelRoot.SetActive(false);

        BuildPanel();
        BindButtons();

        hud = FindFirstObjectByType<GameHUD>();
    }

    private void BuildPanel()
    {
        var canvasGo = new GameObject("ResultPanelCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;   // 模态置顶（HUD 450 / 暂停 800 / 淡出 32767）
        canvasGo.AddComponent<GraphicRaycaster>();   // 缺它则按钮点击/滚轮事件全哑
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        newRoot = new GameObject("Panel", typeof(RectTransform));
        newRoot.transform.SetParent(canvasGo.transform, false);
        var rootRt = (RectTransform)newRoot.transform;
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(980f, 660f);

        // 全屏暗化遮罩（加深：杜绝背景菜单透字）
        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(canvasGo.transform, false);
        var dimRt = (RectTransform)dim.transform;
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        dim.GetComponent<Image>().raycastTarget = false;   // 勿吞事件：HUD已隐藏，挡射线会让按钮/滚轮全罢工

        // 卡片
        var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(newRoot.transform, false);
        var cardRt = (RectTransform)card.transform;
        cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(980f, 660f);
        var cardImg = card.GetComponent<Image>();
        cardImg.color = new Color(0.085f, 0.085f, 0.115f, 1f);   // 不透明：杜绝背景按钮透字
        cardImg.raycastTarget = true;

        // 标题（胜利绿/失败红由 Show 指定）——先设锚点再设位置（锚点顺序坑：后改锚点会使旧 pos 语义漂移）
        newTitle = CreateText(card.transform, "Title", "", 46, FontStyles.Bold, Vector2.zero, new Vector2(860f, 64f));
        newTitle.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        newTitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        newTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        newTitle.rectTransform.anchoredPosition = new Vector2(0f, -30f);

        // 副标题（用时/死因，由 detail 首行承载——不单独拆，整块进滚动区）

        // 滚动详情区
        var scrollGo = new GameObject("DetailScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(card.transform, false);
        var scrollRt = (RectTransform)scrollGo.transform;
        scrollRt.anchorMin = new Vector2(0.5f, 1f);
        scrollRt.anchorMax = new Vector2(0.5f, 1f);
        scrollRt.pivot = new Vector2(0.5f, 1f);
        scrollRt.anchoredPosition = new Vector2(0f, -96f);
        scrollRt.sizeDelta = new Vector2(880f, 408f);
        var scrollImg = scrollGo.GetComponent<Image>();
        scrollImg.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        scrollImg.raycastTarget = true;
        scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 26f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        // Viewport（裁剪）
        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var vpRt = (RectTransform)viewportGo.transform;
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.pivot = new Vector2(0f, 1f);
        vpRt.offsetMin = new Vector2(14f, 10f);
        vpRt.offsetMax = new Vector2(-26f, -10f);
        viewportGo.GetComponent<Image>().color = Color.clear;
        viewportGo.GetComponent<Image>().raycastTarget = true;

        // Content（高度随文本自适应）
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(vpRt, false);
        scrollContent = (RectTransform)contentGo.transform;
        scrollContent.anchorMin = new Vector2(0f, 1f);
        scrollContent.anchorMax = new Vector2(1f, 1f);
        scrollContent.pivot = new Vector2(0.5f, 1f);
        scrollContent.sizeDelta = new Vector2(0f, 100f);

        // 滚动方式：滚轮 + 拖拽（不挂滚动条——ScrollRect 对手动锚定滚动条的布局驱动不稳定）

        // 详情文本（富文本，随内容增高）
        var textGo = new GameObject("Detail", typeof(RectTransform));
        textGo.transform.SetParent(scrollContent, false);
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = new Vector2(0f, 1f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.pivot = new Vector2(0.5f, 1f);
        textRt.offsetMin = new Vector2(8f, 0f);
        textRt.offsetMax = new Vector2(-8f, 0f);
        newDetail = textGo.AddComponent<TextMeshProUGUI>();
        newDetail.fontSize = 22;
        newDetail.color = new Color(0.92f, 0.92f, 0.94f, 1f);
        newDetail.alignment = TextAlignmentOptions.TopLeft;
        newDetail.raycastTarget = false;
        newDetail.enableWordWrapping = true;

        scroll.content = scrollContent;
        scroll.viewport = vpRt;   // 缺它则滚轮/拖拽的工作区回退到 ScrollRect 自身

        // 底部按钮行
        var retry = CreateButton(card.transform, "BtnRetry", "重新开始",
            new Color(0.91f, 0.45f, 0.16f, 1f), new Vector2(-118f, -258f), new Vector2(212f, 58f));
        var exit = CreateButton(card.transform, "BtnExit", "返回关卡选择",
            new Color(0.16f, 0.17f, 0.22f, 1f), new Vector2(118f, -258f), new Vector2(212f, 58f));

        retryButton = retry;
        exitButton = exit;
        titleText = newTitle;
        detailText = newDetail;

        canvasGo.SetActive(false);
    }

    private void BindButtons()
    {
        retryButton.onClick.AddListener(() =>
        {
            RetryRun.Requested = true;   // 同局重试：保持同一随机起火点与出生点
            RestoreHud();
            SceneLoader.Load(SceneManager.GetActiveScene().name);
        });
        exitButton.onClick.AddListener(() =>
        {
            RestoreHud();
            SceneLoader.Load(SceneNames.LevelSelect);
        });
    }

    public void ShowWin(string detail)
    {
        titleText.text = "逃生成功！";
        titleText.color = new Color(0.35f, 0.9f, 0.45f);
        Show(detail);
    }

    public void ShowLose(string reason, string detail)
    {
        titleText.text = "逃生失败 - " + reason;
        titleText.color = new Color(0.95f, 0.35f, 0.25f);
        Show(detail);
    }

    private void Show(string detail)
    {
        detailText.text = detail;
        // 内容高度自适应（滚动）：+56 底部内边距防尾行裁切
        Canvas.ForceUpdateCanvases();
        float preferred = detailText.preferredHeight;
        scrollContent.sizeDelta = new Vector2(0f, Mathf.Max(preferred, 100f) + 110f);
        scroll.verticalNormalizedPosition = 1f;

        panelRoot?.SetActive(false);   // 旧面板保持退役
        newRoot.transform.parent.gameObject.SetActive(true);
        if (hud == null) hud = FindFirstObjectByType<GameHUD>();
        hud?.SetHudVisible(false);     // 隐藏 HUD（消除右上角重复计时器）
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        RestoreHud();
        if (newRoot != null) newRoot.transform.parent.gameObject.SetActive(false);
        Time.timeScale = 1f;
    }

    private void RestoreHud()
    {
        if (hud == null) hud = FindFirstObjectByType<GameHUD>();
        hud?.SetHudVisible(true);
    }

    // ---------- 构建小工具 ----------

    private TMP_Text CreateText(Transform parent, string name, string text, int size,
        FontStyles style, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = new Color(0.94f, 0.94f, 0.96f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = pos;
        return tmp;
    }

    private Button CreateButton(Transform parent, string name, string label,
        Color color, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.fadeDuration = 0.12f;
        btn.colors = colors;

        var text = CreateText(go.transform, "Label", label, 26, FontStyles.Bold, Vector2.zero, size);
        text.color = Color.white;
        text.raycastTarget = false;
        return btn;
    }
}
