using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 消防知识问答（独立场景 06_Quiz）：题库随机抽题、选项乱序、单选即判；
/// 星级总结 + 错题回顾卡片（错题可跳回科普页对应章节重新学习）。
/// UI 运行时自建「暖色消防学习」风格：暖底 + 圆角卡片 + 橙金交互 + 动效。
/// </summary>
public class QuizModeUI : MonoBehaviour
{
    [Header("内容")]
    [SerializeField] private QuizContent content;
    [Header("背景（可选，留空用纯色）")]
    [SerializeField] private Sprite backgroundSprite;

    // 色板——暗色调（背景图透出+暗色半透明前景+白字可读）
    private static readonly Color BgColor = new Color(0.078f, 0.078f, 0.11f, 1f);          // #14141C 兜底
    private static readonly Color CardColor = new Color(0.10f, 0.10f, 0.14f, 1f);          // 深色卡
    private static readonly Color CardHalfAlpha = new Color(0.10f, 0.10f, 0.14f, 0.85f);   // 深色半透明卡
    private static readonly Color BtnNormalColor = new Color(0.14f, 0.14f, 0.18f, 0.88f);  // 深色按钮
    private static readonly Color OrangeColor = new Color(0.91f, 0.55f, 0.14f, 1f);        // 暖橙按钮
    private static readonly Color AccentColor = new Color(0.961f, 0.62f, 0.176f, 1f);     // #F59E2D
    private static readonly Color CorrectColor = new Color(0.35f, 0.85f, 0.50f, 1f);      // 亮绿（暗底更醒目）
    private static readonly Color WrongColor = new Color(0.95f, 0.35f, 0.30f, 1f);        // 亮红
    private static readonly Color TextGreyColor = new Color(0.62f, 0.62f, 0.68f, 1f);     // 浅灰
    private static readonly Color DotGrey = new Color(0.28f, 0.28f, 0.35f, 1f);            // 暗底灰点
    private static readonly Color TextDarkColor = new Color(0.95f, 0.95f, 0.96f, 1f);      // 白字（暗底用）
    private static readonly Color DimWarm = new Color(0.05f, 0.05f, 0.08f, 0.35f);         // 深色暗化叠层

    private static readonly string[] RatingTexts =
    {
        "先回科普页学习一遍，再回来挑战吧！",
        "基础尚可，建议先复习科普章节再来挑战。",
        "掌握良好，个别知识点建议回顾科普章节补强。",
        "消防知识掌握优秀，可以放心进入关卡实战！",
    };

    private class RoundItem
    {
        public QuizContent.QuizItem item;
        public readonly string[] options = new string[3];
        public int correct;
        public int chosen = -1;
    }

    private readonly List<RoundItem> round = new List<RoundItem>();
    private int index;
    private bool answered;

    private GameObject startView, questionView, summaryView;
    private TMP_Text rulesText;
    private Button startButton;
    private TMP_Text questionText;
    private readonly Button[] optionButtons = new Button[3];
    private readonly Image[] optionImages = new Image[3];
    private readonly TMP_Text[] optionLetters = new TMP_Text[3];
    private readonly Image[] progressDots = new Image[10];
    private TMP_Text scoreCounterText;
    private TMP_Text feedbackText;
    private Button nextButton;
    private TMP_Text starsText, scoreText, ratingText;
    private GameObject wrongScroll;
    private TMP_Text emptyText;
    private RectTransform wrongContent;
    private Coroutine animRoutine;
    private static Sprite _rounded;
    private GameObject quitConfirmPanel;
    private bool quitConfirmShowing; // 圆角 Sprite 缓存（static：CreateRounded 静态方法可访问）

    private void Awake()
    {
        _rounded = CreateRoundedSprite();
        BuildUI();
        ShowStart();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ---------- 圆角 Sprite 生成 ----------

    private Sprite CreateRoundedSprite()
    {
        int sz = 64;
        int corner = 16;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float hw = sz / 2f - 1f, hh = sz / 2f - 1f;
                float dx = Mathf.Abs(x - sz / 2f + 0.5f) - (hw - corner);
                float dy = Mathf.Abs(y - sz / 2f + 0.5f) - (hh - corner);
                float qx = Mathf.Max(dx, 0f), qy = Mathf.Max(dy, 0f);
                float d = Mathf.Sqrt(qx * qx + qy * qy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - corner;
                float a = Mathf.Clamp01(0.5f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(corner, corner, corner, corner));
    }

    // ---------- 视图构建 ----------

    private void BuildUI()
    {
        var canvasGo = new GameObject("QuizCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var bg = CreateImage(canvasGo.transform, "Bg", BgColor);
        Stretch(bg.GetComponent<RectTransform>());

        if (backgroundSprite != null)
        {
            var bgImg = CreateImage(canvasGo.transform, "Background", Color.white);
            Stretch(bgImg.GetComponent<RectTransform>());
            bgImg.GetComponent<Image>().sprite = backgroundSprite;
            var dim = CreateImage(canvasGo.transform, "DimOverlay", DimWarm);
            Stretch(dim.GetComponent<RectTransform>());
        }

        BuildStartView(canvasGo.transform);
        BuildQuestionView(canvasGo.transform);
        BuildSummaryView(canvasGo.transform);
        BuildQuitConfirm(canvasGo.transform);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (questionView != null && questionView.activeSelf && !quitConfirmShowing)
                ShowQuitConfirm();
            else if (quitConfirmShowing)
                HideQuitConfirm();
        }
    }

    private void BuildQuitConfirm(Transform parent)
    {
        quitConfirmPanel = new GameObject("QuitConfirm", typeof(RectTransform));
        Stretch(quitConfirmPanel.GetComponent<RectTransform>());
        quitConfirmPanel.transform.SetParent(parent, false);

        var bg = CreateImage(quitConfirmPanel.transform, "QuitBg", new Color(0, 0, 0, 0.55f));
        Stretch(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().raycastTarget = true;

        var panel = CreateRounded(quitConfirmPanel.transform, "QuitPanel", CardColor);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(480f, 240f);
        panel.GetComponent<Image>().raycastTarget = true;

        CreateText(panel.transform, "QuitText", "确定要退出答题吗？", 28, new Vector2(0f, 50f));

        var btnYes = CreateButton(panel.transform, "BtnYes", "是，退出答题", OrangeColor, new Vector2(-105f, -55f));
        btnYes.GetComponent<RectTransform>().sizeDelta = new Vector2(190f, 48f);
        btnYes.onClick.AddListener(QuitToSummary);

        var btnNo = CreateButton(panel.transform, "BtnNo", "否，继续答题", BtnNormalColor, new Vector2(105f, -55f));
        btnNo.GetComponent<RectTransform>().sizeDelta = new Vector2(190f, 48f);
        btnNo.onClick.AddListener(HideQuitConfirm);

        quitConfirmPanel.SetActive(false);
    }

    private void ShowQuitConfirm()
    {
        quitConfirmShowing = true;
        quitConfirmPanel.SetActive(true);
    }

    private void HideQuitConfirm()
    {
        quitConfirmShowing = false;
        quitConfirmPanel.SetActive(false);
    }

    private void QuitToSummary()
    {
        HideQuitConfirm();
        ShowSummary();
    }

    private void BuildStartView(Transform parent)
    {
        startView = new GameObject("StartView", typeof(RectTransform));
        Stretch(startView.GetComponent<RectTransform>());
        startView.transform.SetParent(parent, false);

        var title = CreateText(startView.transform, "TitleText", "消防知识问答", 64, new Vector2(0f, 260f));
        title.color = AccentColor;
        title.fontStyle = FontStyles.Bold;
        if (title.gameObject.GetComponent<Shadow>() == null)
        { var sh = title.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.3f); sh.effectDistance = new Vector2(0, -2f); }

        var accentBar = CreateImage(startView.transform, "AccentBar", AccentColor);
        accentBar.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 5f);
        accentBar.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 188f);

        rulesText = CreateText(startView.transform, "RulesText", "", 28, new Vector2(0f, 30f));
        rulesText.color = TextGreyColor;
        rulesText.rectTransform.sizeDelta = new Vector2(1080f, 240f);

        startButton = CreateButton(startView.transform, "BtnStart", "开始答题", OrangeColor, new Vector2(0f, -170f));
        startButton.GetComponent<RectTransform>().sizeDelta = new Vector2(480f, 68f);
        startButton.GetComponentInChildren<TMP_Text>().fontSize = 34;
        startButton.onClick.AddListener(StartRound);

        Button back = CreateButton(startView.transform, "BtnHome", "返回主菜单", BtnNormalColor, new Vector2(0f, -270f));
        back.onClick.AddListener(BackToMenu);
    }

    private void BuildQuestionView(Transform parent)
    {
        questionView = new GameObject("QuestionView", typeof(RectTransform));
        Stretch(questionView.GetComponent<RectTransform>());
        questionView.transform.SetParent(parent, false);

        // 进度圆点
        for (int i = 0; i < 10; i++)
        {
            var dot = CreateImage(questionView.transform, "Dot" + i, DotGrey);
            var drt = dot.GetComponent<RectTransform>();
            drt.sizeDelta = new Vector2(20f, 20f);
            drt.anchoredPosition = new Vector2(-162f + i * 36f, 460f);
            progressDots[i] = dot.GetComponent<Image>();
        }

        var progLabel = CreateText(questionView.transform, "ProgressLabel", "", 24, new Vector2(0f, 424f));
        progLabel.color = TextGreyColor;
        progLabel.name = "ProgressText";

        // 题目卡（圆角暖色）
        var card = CreateImage(questionView.transform, "QuestionCard", CardHalfAlpha);
        card.GetComponent<Image>().sprite = _rounded;
        card.GetComponent<Image>().type = Image.Type.Sliced;
        card.GetComponent<RectTransform>().sizeDelta = new Vector2(1060f, 720f);
        card.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -8f);

        var badge = CreateImage(card.transform, "QBadge", OrangeColor);
        badge.GetComponent<RectTransform>().sizeDelta = new Vector2(56f, 40f);
        badge.GetComponent<RectTransform>().anchoredPosition = new Vector2(-470f, 334f);
        var badgeText = CreateText(badge.transform, "QLabel", "Q1", 24, Vector2.zero);
        badgeText.color = Color.white;
        badgeText.fontStyle = FontStyles.Bold;

        var quizImage = CreateImage(card.transform, "QuizImage", Color.white).GetComponent<Image>();
        quizImage.rectTransform.sizeDelta = new Vector2(500f, 250f);
        quizImage.rectTransform.anchoredPosition = new Vector2(0f, 218f);

        questionText = CreateText(card.transform, "QuestionText", "", 34, new Vector2(0f, 35f));
        questionText.rectTransform.sizeDelta = new Vector2(980f, 190f);

        for (int i = 0; i < 3; i++)
        {
            var optRoot = new GameObject("OptionRoot" + i, typeof(RectTransform));
            optRoot.transform.SetParent(card.transform, false);
            var ort = (RectTransform)optRoot.transform;
            ort.sizeDelta = new Vector2(840f, 60f);
            ort.anchoredPosition = new Vector2(0f, -98f - i * 74f);

            var letterBg = CreateImage(optRoot.transform, "LetterBg", new Color(0.95f, 0.82f, 0.60f, 1f));
            letterBg.GetComponent<RectTransform>().sizeDelta = new Vector2(46f, 46f);
            letterBg.GetComponent<RectTransform>().anchoredPosition = new Vector2(-385f, 0f);
            var letter = CreateText(letterBg.transform, "Letter", ((char)('A' + i)).ToString(), 26, Vector2.zero);
            letter.color = TextDarkColor;
            letter.fontStyle = FontStyles.Bold;
            optionLetters[i] = letter;

            Button opt = CreateButton(optRoot.transform, "Option" + i, "", BtnNormalColor, new Vector2(40f, 0f));
            opt.GetComponent<RectTransform>().sizeDelta = new Vector2(680f, 60f);
            opt.GetComponentInChildren<TMP_Text>().fontSize = 26;
            int captured = i;
            opt.onClick.AddListener(() => Answer(captured));
            optionButtons[i] = opt;
            optionImages[i] = opt.GetComponent<Image>();
            var btnColors = opt.colors;
            btnColors.highlightedColor = new Color(0.98f, 0.90f, 0.76f, 1f);
            btnColors.pressedColor = new Color(0.90f, 0.82f, 0.65f, 1f);
            btnColors.fadeDuration = 0.12f;
            opt.colors = btnColors;
        }

        feedbackText = CreateText(card.transform, "FeedbackText", "", 24, new Vector2(0f, -330f));
        feedbackText.rectTransform.sizeDelta = new Vector2(1000f, 120f);
        var fbBg = CreateImage(card.transform, "FeedbackBg", new Color(0.08f, 0.08f, 0.12f, 0.75f));
        fbBg.GetComponent<Image>().sprite = _rounded;
        fbBg.GetComponent<Image>().type = Image.Type.Sliced;
        fbBg.GetComponent<RectTransform>().sizeDelta = new Vector2(1020f, 90f);
        fbBg.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -330f);
        fbBg.transform.SetSiblingIndex(feedbackText.transform.GetSiblingIndex());
        fbBg.gameObject.SetActive(false); // 初始隐藏

        scoreCounterText = CreateText(questionView.transform, "ScoreCounter", "", 22, new Vector2(430f, -492f));
        scoreCounterText.color = AccentColor;

        nextButton = CreateButton(questionView.transform, "BtnNext", "下一题", OrangeColor, new Vector2(0f, -455f));
        nextButton.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 58f);
        nextButton.onClick.AddListener(Next);
    }

    private void BuildSummaryView(Transform parent)
    {
        summaryView = new GameObject("SummaryView", typeof(RectTransform));
        Stretch(summaryView.GetComponent<RectTransform>());
        summaryView.transform.SetParent(parent, false);

        var title = CreateText(summaryView.transform, "TitleText", "答题结束", 46, new Vector2(0f, 468f));
        title.color = new Color(0.2f, 0.15f, 0.1f, 1f);

        starsText = CreateText(summaryView.transform, "StarsText", "", 64, new Vector2(0f, 388f));
        if (starsText.gameObject.GetComponent<Shadow>() == null)
        { var sh = starsText.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.3f); sh.effectDistance = new Vector2(0, -2f); }
        scoreText = CreateText(summaryView.transform, "ScoreText", "", 30, new Vector2(0f, 316f));
        scoreText.color = TextDarkColor;
        ratingText = CreateText(summaryView.transform, "RatingText", "", 24, new Vector2(0f, 262f));
        ratingText.color = TextGreyColor;
        ratingText.rectTransform.sizeDelta = new Vector2(900f, 50f);

        var scrollGo = new GameObject("WrongScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(summaryView.transform, false);
        wrongScroll = scrollGo;
        var scrollRt = (RectTransform)scrollGo.transform;
        scrollRt.sizeDelta = new Vector2(1120f, 545f);
        scrollRt.anchoredPosition = new Vector2(0f, -42f);
        scrollGo.GetComponent<Image>().color = new Color(0.97f, 0.94f, 0.88f, 0.9f);

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var viewportRt = (RectTransform)viewportGo.transform;
        Stretch(viewportRt);
        viewportGo.GetComponent<Image>().color = Color.white;
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("Content", typeof(RectTransform));
        wrongContent = (RectTransform)contentGo.transform;
        wrongContent.SetParent(viewportGo.transform, false);
        wrongContent.anchorMin = new Vector2(0f, 1f);
        wrongContent.anchorMax = new Vector2(1f, 1f);
        wrongContent.pivot = new Vector2(0.5f, 1f);
        wrongContent.sizeDelta = new Vector2(0f, 0f);

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewportRt;
        scroll.content = wrongContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 30f;

        var sbGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        sbGo.transform.SetParent(scrollGo.transform, false);
        var sbRt = (RectTransform)sbGo.transform;
        sbRt.anchorMin = new Vector2(1f, 0f);
        sbRt.anchorMax = new Vector2(1f, 1f);
        sbRt.offsetMin = new Vector2(-18f, 4f);
        sbRt.offsetMax = new Vector2(-4f, -4f);
        sbGo.GetComponent<Image>().color = new Color(0.90f, 0.86f, 0.78f, 1f);
        var sb = sbGo.GetComponent<Scrollbar>();

        var areaGo = new GameObject("SlidingArea", typeof(RectTransform));
        areaGo.transform.SetParent(sbGo.transform, false);
        var areaRt = (RectTransform)areaGo.transform;
        areaRt.anchorMin = Vector2.zero;
        areaRt.anchorMax = Vector2.one;
        areaRt.offsetMin = new Vector2(3f, 3f);
        areaRt.offsetMax = new Vector2(-3f, -3f);

        var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(areaGo.transform, false);
        var hRt = (RectTransform)handleGo.transform;
        hRt.sizeDelta = Vector2.zero;
        var hImg = handleGo.GetComponent<Image>();
        hImg.color = new Color(0.75f, 0.65f, 0.50f, 1f);

        sb.targetGraphic = hImg;
        sb.handleRect = hRt;
        sb.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = sb;

        emptyText = CreateText(summaryView.transform, "EmptyText", "", 28, new Vector2(0f, -42f));
        emptyText.color = CorrectColor;
        emptyText.rectTransform.sizeDelta = new Vector2(1000f, 200f);

        Button retry = CreateButton(summaryView.transform, "BtnRetry", "再来一轮", OrangeColor, new Vector2(-190f, -455f));
        retry.GetComponent<RectTransform>().sizeDelta = new Vector2(340f, 58f);
        retry.onClick.AddListener(StartRound);

        Button home = CreateButton(summaryView.transform, "BtnHome", "返回主菜单", BtnNormalColor, new Vector2(190f, -455f));
        home.onClick.AddListener(BackToMenu);
    }

    // ---------- 流程控制 ----------

    private void ShowStart()
    {
        startView.SetActive(true);
        questionView.SetActive(false);
        summaryView.SetActive(false);

        bool ready = content != null && content.questions.Count > 0;
        if (rulesText != null)
        {
            rulesText.text = ready
                ? $"题库共 {content.questions.Count} 题 · 每轮随机抽取 {Mathf.Min(content.roundSize, content.questions.Count)} 题\n选项顺序随机 · 单选即判，答错不得分\n答对 9-10 题 ★★★　7-8 题 ★★　5-6 题 ★"
                : "题库尚未配置，请检查 Quiz_Content 资产。";
        }
        if (startButton != null) startButton.interactable = ready;
    }

    public void StartRound()
    {
        if (content == null || content.questions.Count == 0) return;

        var pool = new List<QuizContent.QuizItem>(content.questions);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int count = Mathf.Clamp(content.roundSize, 1, pool.Count);
        round.Clear();
        for (int i = 0; i < count; i++)
        {
            var r = new RoundItem { item = pool[i] };
            ShuffleOptions(r);
            round.Add(r);
        }
        index = 0;

        startView.SetActive(false);
        summaryView.SetActive(false);
        questionView.SetActive(true);
        ShowQuestion();
    }

    private void ShuffleOptions(RoundItem r)
    {
        int[] order = { 0, 1, 2 };
        for (int i = 2; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        for (int pos = 0; pos < 3; pos++)
        {
            r.options[pos] = OptionText(r.item, order[pos]);
            if (order[pos] == r.item.correctIndex) r.correct = pos;
        }
    }

    private static string OptionText(QuizContent.QuizItem item, int i)
    {
        return i == 0 ? item.optionA : i == 1 ? item.optionB : item.optionC;
    }

    private void ShowQuestion()
    {
        var r = round[index];
        answered = false;
        if (animRoutine != null) { StopCoroutine(animRoutine); animRoutine = null; }

        // 进度圆点复位（当前题=白色高亮）
        for (int i = 0; i < progressDots.Length; i++)
        {
            if (i == index) { progressDots[i].color = Color.white; continue; }
            progressDots[i].color = i < index ? (round[i].chosen >= 0 && round[i].chosen == round[i].correct ? CorrectColor : WrongColor) : DotGrey;
        }

        var badgeText = questionView.transform.Find("QuestionCard")?.Find("QBadge")?.Find("QLabel")?.GetComponent<TMP_Text>();
        if (badgeText != null) badgeText.text = "Q" + (index + 1);

        var qImg = questionView.transform.Find("QuestionCard")?.Find("QuizImage")?.GetComponent<Image>();
        bool hasIll = r.item.illustration != null;
        if (qImg != null)
        {
            qImg.gameObject.SetActive(hasIll);
            if (hasIll) qImg.sprite = r.item.illustration;
        }
        questionText.rectTransform.anchoredPosition = hasIll ? new Vector2(0f, 35f) : new Vector2(0f, 235f);
        questionText.text = r.item.question;

        for (int i = 0; i < 3; i++)
        {
            optionButtons[i].interactable = true;
            optionImages[i].color = BtnNormalColor;
            optionLetters[i].color = TextDarkColor;
            optionButtons[i].GetComponentInChildren<TMP_Text>().text = r.options[i];
        }

        feedbackText.text = "";
        var fbBgH = questionView.transform.Find("QuestionCard")?.Find("FeedbackBg");
        if (fbBgH != null) fbBgH.gameObject.SetActive(false);
        var progLabel = questionView.transform.Find("ProgressText")?.GetComponent<TMP_Text>();
        if (progLabel != null) progLabel.text = $"第 {index + 1} / {round.Count} 题";
        nextButton.gameObject.SetActive(false);
        UpdateScoreCounter();
    }

    private void UpdateScoreCounter()
    {
        int correctSoFar = 0;
        for (int i = 0; i < index && i < round.Count; i++)
            if (round[i].chosen >= 0 && round[i].chosen == round[i].correct) correctSoFar++;
        if (scoreCounterText != null) scoreCounterText.text = $"已答对 {correctSoFar} 题";
    }

    private void Answer(int pos)
    {
        if (answered || pos < 0 || pos > 2) return;
        answered = true;

        var r = round[index];
        r.chosen = pos;
        bool correct = pos == r.correct;

        for (int i = 0; i < 3; i++) optionButtons[i].interactable = false;
        optionImages[r.correct].color = CorrectColor;
        optionLetters[r.correct].color = Color.white;
        if (!correct)
        {
            optionImages[pos].color = WrongColor;
            optionLetters[pos].color = Color.white;
        }

        if (index < progressDots.Length)
            progressDots[index].color = correct ? CorrectColor : WrongColor;

        feedbackText.color = correct ? CorrectColor : WrongColor;
        feedbackText.text = (correct ? "[√] 回答正确！" : "[×] 回答错误。") + r.item.explanation;
        var fbBgT = questionView.transform.Find("QuestionCard")?.Find("FeedbackBg");
        if (fbBgT != null) fbBgT.gameObject.SetActive(true);

        bool last = index >= round.Count - 1;
        nextButton.GetComponentInChildren<TMP_Text>().text = last ? "查看总结" : "下一题";
        nextButton.gameObject.SetActive(true);
        UpdateScoreCounter();

        if (correct) animRoutine = StartCoroutine(AutoAdvance(1.5f));
    }

    private IEnumerator AutoAdvance(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Next();
    }

    private void Next()
    {
        if (!answered) return;
        if (animRoutine != null) { StopCoroutine(animRoutine); animRoutine = null; }
        index++;
        if (index >= round.Count) ShowSummary();
        else ShowQuestion();
    }

    // ---------- 总结 ----------

    private void ShowSummary()
    {
        int correctCount = 0;
        foreach (var r in round)
            if (r.chosen >= 0 && r.chosen == r.correct) correctCount++;

        int stars = correctCount >= 9 ? 3 : correctCount >= 7 ? 2 : correctCount >= 5 ? 1 : 0;

        string hex = ColorUtility.ToHtmlStringRGB(AccentColor);
        starsText.text = "";
        scoreText.text = $"答对 {correctCount} / {round.Count} 题";
        ratingText.text = RatingTexts[stars];

        BuildWrongCards();

        questionView.SetActive(false);
        summaryView.SetActive(true);

        animRoutine = StartCoroutine(RevealStars(stars, hex));
    }

    private IEnumerator RevealStars(int stars, string hex)
    {
        yield return new WaitForSecondsRealtime(0.3f);
        for (int i = 0; i < stars; i++)
        {
            string current = "<color=#" + hex + ">" + new string('★', i + 1) + "</color>" +
                             "<color=#C8BFA8>" + new string('☆', 3 - i - 1) + "</color>";
            starsText.text = current;
            starsText.transform.localScale = Vector3.one * 1.3f;
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                starsText.transform.localScale = Vector3.Lerp(Vector3.one * 1.3f, Vector3.one, t / 0.25f);
                yield return null;
            }
            starsText.transform.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(0.4f);
        }
        // 最终状态固定为实际星数
        starsText.text = "<color=#" + hex + ">" + new string('★', stars) + "</color>" +
                         "<color=#C8BFA8>" + new string('☆', 3 - stars) + "</color>";
        animRoutine = null;
    }

    private void BuildWrongCards()
    {
        for (int i = wrongContent.childCount - 1; i >= 0; i--)
            DestroyImmediate(wrongContent.GetChild(i).gameObject);

        int wrongCount = 0;
        foreach (var r in round)
            if (r.chosen < 0 || r.chosen != r.correct) wrongCount++;

        bool anyWrong = wrongCount > 0;
        wrongScroll.SetActive(anyWrong);
        emptyText.gameObject.SetActive(!anyWrong);
        if (!anyWrong)
        {
            emptyText.text = "全部答对，没有错题！\n消防知识掌握得非常扎实，放心进入关卡实战吧。";
            return;
        }

        const float cardHeight = 312f;
        wrongContent.sizeDelta = new Vector2(0f, wrongCount * cardHeight);

        int number = 0;
        foreach (var r in round)
        {
            if (r.chosen >= 0 && r.chosen == r.correct) continue;
            number++;
            var card = CreateImage(wrongContent, "WrongCard" + number, CardColor);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(1096f, 300f);
            rt.anchoredPosition = new Vector2(0f, -(number - 1) * cardHeight - 6f);

            TMP_Text q = CreateText(card.transform, "QuestionText", number + ". " + r.item.question, 26, new Vector2(0f, 112f));
            q.alignment = TextAlignmentOptions.TopLeft;
            q.color = TextDarkColor;
            q.rectTransform.sizeDelta = new Vector2(1030f, 76f);

            TMP_Text your = CreateText(card.transform, "YourText", "你的答案：" + (r.chosen >= 0 ? r.options[r.chosen] : "未作答"), 22, new Vector2(0f, 44f));
            your.color = WrongColor;
            your.alignment = TextAlignmentOptions.TopLeft;
            your.rectTransform.sizeDelta = new Vector2(1030f, 40f);

            TMP_Text right = CreateText(card.transform, "CorrectText", "正确答案：" + r.options[r.correct], 22, new Vector2(0f, 6f));
            right.color = CorrectColor;
            right.alignment = TextAlignmentOptions.TopLeft;
            right.rectTransform.sizeDelta = new Vector2(1030f, 40f);

            TMP_Text explain = CreateText(card.transform, "ExplainText", r.item.explanation, 21, new Vector2(0f, -62f));
            explain.color = TextGreyColor;
            explain.alignment = TextAlignmentOptions.TopLeft;
            explain.rectTransform.sizeDelta = new Vector2(1030f, 120f);

            string chapter = ChapterName(r.item.scienceCategoryIndex);
            Button jump = CreateButton(card.transform, "BtnChapter", "查看科普 · " + chapter, OrangeColor, new Vector2(0f, -118f));
            jump.GetComponent<RectTransform>().sizeDelta = new Vector2(340f, 44f);
            jump.GetComponentInChildren<TMP_Text>().fontSize = 22;
            int category = r.item.scienceCategoryIndex;
            jump.onClick.AddListener(() =>
            {
                FireScienceUI.pendingCategoryIndex = category;
                SceneLoader.Load(SceneNames.FireScience);
            });
        }

        var scroll = wrongScroll.GetComponent<ScrollRect>();
        scroll.verticalNormalizedPosition = 1f;
    }

    private static string ChapterName(int i)
    {
        string[] names = { "火灾常识", "报警求助", "灭火器使用", "逃生要点", "常见误区" };
        return i >= 0 && i < names.Length ? names[i] : "火灾常识";
    }

    private void BackToMenu()
    {
        SceneLoader.Load(SceneNames.MainMenu);
    }

    // ---------- UI 构建小工具 ----------

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
        go.GetComponent<Image>().raycastTarget = false;
        return go;
    }

    /// <summary>创建带圆角的 Image（9-slice 拉伸，颜色可调）。</summary>
    private static GameObject CreateRounded(Transform parent, string name, Color color)
    {
        var go = CreateImage(parent, name, color);
        var img = go.GetComponent<Image>();
        img.sprite = _rounded;
        img.type = Image.Type.Sliced;
        return go;
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, int size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = new Color(0.95f, 0.95f, 0.96f, 1f);
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
        GameObject go = CreateImage(parent, name, color);
        var img = go.GetComponent<Image>();
        img.sprite = _rounded;
        img.type = Image.Type.Sliced;
        go.GetComponent<Image>().raycastTarget = true;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400f, 64f);
        rt.anchoredPosition = pos;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        TMP_Text text = CreateText(go.transform, "Label", label, 30, Vector2.zero);
        text.color = Color.white;
        text.rectTransform.sizeDelta = rt.sizeDelta;
        return btn;
    }
}
