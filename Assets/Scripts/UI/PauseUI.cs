using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Esc 真暂停：冻结 timeScale（火势/计时/教学全部停走）并弹出暂停面板，按钮点击立即生效。
/// 由 FirstPersonController 在 Awake 时挂载（仅关卡场景存在），面板运行时自建、零场景改动。
/// 问答/119报警/结算面板持有 timeScale=0 期间 Esc 无效，避免暂停状态互相打架。
/// </summary>
public class PauseUI : MonoBehaviour
{
    [Header("样式（配色与科普关/主菜单一致）")]
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color panelColor = new Color(0.13f, 0.14f, 0.18f, 0.97f);
    [SerializeField] private Color resumeButtonColor = new Color(0.91f, 0.38f, 0.18f, 1f);
    [SerializeField] private Color exitButtonColor = new Color(0.16f, 0.17f, 0.22f, 1f);

    public bool Paused => paused;

    private bool paused;
    private GameObject panelRoot;

    private void Awake()
    {
        BuildPanel();
    }

    /// <summary>Esc 切换：自己持有暂停时切换；其他模态（问答/结算）持有 timeScale=0 时忽略。</summary>
    public void Toggle()
    {
        if (paused) Resume();
        else if (Time.timeScale > 0.01f) Pause();
    }

    private void Update()
    {
        // Esc 轮询放在本组件：Update 不受 timeScale 影响，暂停中再按 Esc 才能恢复；
        // 由玩家对象挂载（仅关卡场景），菜单场景天然无此逻辑
        if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
    }

    private void Pause()
    {
        paused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        if (panelRoot != null) panelRoot.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Resume()
    {
        paused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ExitToLevelSelect()
    {
        // SceneLoader 协程会复位 timeScale=1，这里只需防止静音泄漏到菜单场景
        AudioListener.pause = false;
        SceneLoader.Load(SceneNames.LevelSelect);
    }

    private void BuildPanel()
    {
        var canvasGo = new GameObject("PauseCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;   // 高于 HUD，低于 SceneLoader 淡出层(32767)
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 全屏遮罩：压暗 + 挡住底下 HUD 的点击
        panelRoot = new GameObject("PausePanel");
        panelRoot.transform.SetParent(canvasGo.transform, false);
        var overlay = panelRoot.AddComponent<Image>();
        overlay.color = overlayColor;
        Stretch(panelRoot.GetComponent<RectTransform>());

        var card = CreateImage(panelRoot.transform, "Card", panelColor);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.sizeDelta = new Vector2(480f, 380f);

        CreateText(card.transform, "TitleText", "已暂停", 44, new Vector2(0f, 118f));

        Button resumeBtn = CreateButton(card.transform, "BtnResume", "继续游戏", resumeButtonColor, new Vector2(0f, 8f));
        resumeBtn.onClick.AddListener(Resume);

        Button exitBtn = CreateButton(card.transform, "BtnExit", "返回关卡选择", exitButtonColor, new Vector2(0f, -102f));
        exitBtn.onClick.AddListener(ExitToLevelSelect);

        panelRoot.SetActive(false);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, int size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var tmp = go.AddComponent<TextMeshProUGUI>();   // 默认字体经 TMP Settings fallback 渲染中文（NotoSansSC）
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = new Color(0.95f, 0.95f, 0.96f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var rt = tmp.rectTransform;
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(420f, 60f);
        rt.anchoredPosition = pos;
        return tmp;
    }

    private static Button CreateButton(Transform parent, string name, string label, Color color, Vector2 pos)
    {
        var go = CreateImage(parent, name, color);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(380f, 64f);
        rt.anchoredPosition = pos;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        TMP_Text text = CreateText(go.transform, "Label", label, 30, Vector2.zero);
        text.color = Color.white;
        return btn;
    }
}
