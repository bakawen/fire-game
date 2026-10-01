using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>火灾科普界面：左侧分类导航 + 右侧图文卡片流 + 序列帧演示动画（自测问答已独立为 QuizModeUI / 06_Quiz 场景）。</summary>
public class FireScienceUI : MonoBehaviour
{
    /// <summary>跳转定位：答题总结页点"查看科普"前设置目标分类索引，本页 Start 消费后复位（-1=默认第一分类）。</summary>
    public static int pendingCategoryIndex = -1;

    [Header("内容")]
    [SerializeField] private FireScienceContent content;

    [Header("导航")]
    [SerializeField] private Button backButton;
    [SerializeField] private RectTransform sidebar;
    [SerializeField] private Button categoryTemplate;

    [Header("分类视图")]
    [SerializeField] private GameObject categoryView;
    [SerializeField] private TMP_Text categoryTitleText;
    [SerializeField] private TMP_Text categoryIntroText;
    [SerializeField] private ScrollRect cardScroll;
    [SerializeField] private RectTransform cardContainer;
    [SerializeField] private GameObject cardTemplate;
    [Header("滚轮速度（默认值 1 对长卡片流过慢）")]
    [SerializeField] private float cardScrollSensitivity = 8f;

    [Header("演示动画视图（序列帧 uvRect 步进）")]
    [SerializeField] private GameObject animView;
    [SerializeField] private RawImage animRawImage;
    [SerializeField] private TMP_Text animStepText;
    [SerializeField] private Button animToggleButton;
    [TextArea(1, 2)] [SerializeField] private string[] animStepTexts =
        { "① 提起灭火器", "② 拔掉保险销", "③ 握住喷管对准火焰根部", "④ 压下压把持续喷射" };
    [SerializeField] private int[] animStepStarts = { 0, 2, 8, 12 };   // 每步起始帧（按生成图实测分布）
    [SerializeField] private float animFps = 4f;

    // 配色与主菜单/结算面板一致
    private static readonly Color SelectedColor = new Color(0.91f, 0.38f, 0.18f, 1f);
    private static readonly Color NormalColor = new Color(0.16f, 0.17f, 0.22f, 1f);

    private const int AnimFrameCount = 16;      // 4×4 序列图
    private const float Cell = 0.25f;           // 单帧 uv 占比
    private const float CellInset = 8f / 1024f; // 帧内缩：裁掉相邻帧渗透边缘（1024 图上约 8px）

    private readonly List<Button> sidebarButtons = new List<Button>();
    private readonly List<GameObject> cardInstances = new List<GameObject>();
    private int selected = -1;      // >=0=分类索引；-2=演示动画
    private bool animPaused;
    private float animTimer;
    private int animFrame = -1;

    private void Awake()
    {
        if (cardScroll != null) cardScroll.scrollSensitivity = cardScrollSensitivity;
        backButton.onClick.AddListener(() => SceneLoader.Load(SceneNames.MainMenu));
        animToggleButton.onClick.AddListener(ToggleAnim);
        BuildSidebar();
        SelectCategory(0);
    }

    private void Start()
    {
        // 菜单类场景必须释放鼠标（可能从锁定状态的场景切换而来）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 错题"重新科普"跳转：直接定位到对应章节
        if (pendingCategoryIndex >= 0 && pendingCategoryIndex < content.categories.Count)
        {
            SelectCategory(pendingCategoryIndex);
            pendingCategoryIndex = -1;
        }
    }

    private void BuildSidebar()
    {
        for (int i = 0; i < content.categories.Count; i++)
        {
            int index = i;
            Button btn = Instantiate(categoryTemplate, sidebar, false);
            btn.name = "BtnCat_" + i;
            btn.gameObject.SetActive(true);
            btn.GetComponentInChildren<TMP_Text>().text = content.categories[i].title;
            btn.onClick.AddListener(() => SelectCategory(index));
            sidebarButtons.Add(btn);
        }
        Button animBtn = Instantiate(categoryTemplate, sidebar, false);
        animBtn.name = "BtnAnim";
        animBtn.gameObject.SetActive(true);
        animBtn.GetComponentInChildren<TMP_Text>().text = "演示动画";
        animBtn.onClick.AddListener(SelectAnim);
        sidebarButtons.Add(animBtn);
    }

    private void SelectCategory(int index)
    {
        selected = index;
        categoryView.SetActive(true);
        animView.SetActive(false);

        var cat = content.categories[index];
        categoryTitleText.text = cat.title;
        categoryIntroText.text = cat.intro;

        foreach (GameObject card in cardInstances) Destroy(card);
        cardInstances.Clear();
        foreach (var card in cat.cards)
        {
            GameObject go = Instantiate(cardTemplate, cardContainer, false);
            go.name = "Card";
            go.SetActive(true);
            go.transform.Find("HeadingText").GetComponent<TMP_Text>().text = card.heading;
            go.transform.Find("BodyText").GetComponent<TMP_Text>().text = card.body;

            TMP_Text practice = go.transform.Find("PracticeText").GetComponent<TMP_Text>();
            bool hasPractice = !string.IsNullOrEmpty(card.practice);
            practice.gameObject.SetActive(hasPractice);
            if (hasPractice) practice.text = "▶ " + card.practice;

            Image ill = go.transform.Find("Illustration").GetComponent<Image>();
            bool hasIll = card.illustration != null;
            ill.gameObject.SetActive(hasIll);
            if (hasIll) ill.sprite = card.illustration;

            cardInstances.Add(go);
        }
        Canvas.ForceUpdateCanvases();
        cardScroll.verticalNormalizedPosition = 1f;
        RefreshSidebar();
    }

    private void SelectAnim()
    {
        selected = -2;
        categoryView.SetActive(false);
        animView.SetActive(true);
        animTimer = 0f;
        animFrame = -1;
        animPaused = false;
        UpdateAnimToggleLabel();
        RefreshSidebar();
    }

    private void Update()
    {
        // 序列帧步进：uvRect 在 4×4 网格上逐帧推进，步骤字幕同步切换（暂停即冻结）
        if (animView == null || !animView.activeSelf || animPaused) return;
        animTimer += Time.deltaTime;
        int frame = Mathf.FloorToInt(animTimer * animFps) % AnimFrameCount;
        if (frame == animFrame) return;
        animFrame = frame;
        // 序列图第 0 帧在左上角：列 x=col*0.25，行 y=0.75-row*0.25（uv 原点在左下）；四边内缩裁掉相邻帧渗透
        animRawImage.uvRect = new Rect(
            frame % 4 * Cell + CellInset,
            0.75f - frame / 4 * Cell + CellInset,
            Cell - 2 * CellInset,
            Cell - 2 * CellInset);
        int step = 0;
        for (int s = 0; s < animStepStarts.Length && frame >= animStepStarts[s]; s++) step = s;
        animStepText.text = animStepTexts[Mathf.Min(step, animStepTexts.Length - 1)];
    }

    private void ToggleAnim()
    {
        animPaused = !animPaused;
        UpdateAnimToggleLabel();
    }

    private void UpdateAnimToggleLabel()
    {
        if (animToggleButton != null)
            animToggleButton.GetComponentInChildren<TMP_Text>().text = animPaused ? "继续" : "暂停";
    }

    private void RefreshSidebar()
    {
        int animIndexInSidebar = content.categories.Count;   // 演示动画=最后一项
        for (int i = 0; i < sidebarButtons.Count; i++)
        {
            bool active = i == selected || (i == animIndexInSidebar && selected == -2);
            sidebarButtons[i].GetComponent<Image>().color = active ? SelectedColor : NormalColor;
        }
    }
}
