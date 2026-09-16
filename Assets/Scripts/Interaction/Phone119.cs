using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 119报警话术系统（公寓关特色交互）：两轮四选一（先报地址→补充楼层/被困人数/火势），
/// 全部答对=报警成功（触发OnAlarmRaised供LevelFlow推进），选错给正确知识点可重选。
/// 交互对象=卧室床头手机（按E弹出话术面板，timeScale=0防走动）。
/// </summary>
public class Phone119 : MonoBehaviour, IInteractable
{
    [System.Serializable]
    public class QuizRound
    {
        [TextArea(2, 3)] public string prompt;
        [TextArea(1, 2)] public string optionA;
        [TextArea(1, 2)] public string optionB;
        [TextArea(1, 2)] public string optionC;
        [TextArea(1, 2)] public string optionD;
        public int correctIndex;
        [TextArea(1, 3)] public string correction;
    }

    [Header("UI引用")]
    [SerializeField] private GameObject quizPanel;
    [SerializeField] private Text promptText;
    [SerializeField] private Text feedbackText;
    [SerializeField] private Button option0;
    [SerializeField] private Button option1;
    [SerializeField] private Button option2;
    [SerializeField] private Button option3;

    [Header("话术两轮")]
    [SerializeField] private QuizRound round1 = new QuizRound
    {
        prompt = "电话接通。接线员：“你好，119。请讲。”\n你第一句应该说什么？",
        optionA = "“着火了！着火了！快来人啊！”",
        optionB = "“XX路XX小区X栋X单元，X楼住宅起火。”",
        optionC = "“我家人被困了，你们快点来！”",
        optionD = "“请问是消防队吗？”"
    };
    [SerializeField] private QuizRound round2 = new QuizRound
    {
        prompt = "接线员：“好的，具体位置和现场情况请补充。”\n还应该说什么？",
        optionA = "“很严重！整栋楼都要烧起来了！”",
        optionB = "“我先挂了，等你们来再说。”",
        optionC = "“3楼起火，家里2人被困阳台，楼下楼道全是烟。”",
        optionD = "“我在X栋X单元，你们到了给我打电话。”"
    };

    [Header("完成后提示")]
    [SerializeField, TextArea(1, 3)] private string doneHint = "报警成功：说清了地址、楼层、火势和被困人数。消防队已在路上！";

    public string PromptText => alarmRaised ? null : "按 E 拨打 119 报警";
    public bool CanInteract => !alarmRaised;

    /// <summary>119报警完成时触发（LevelFlow 订阅推进阶段）。</summary>
    public event Action<Phone119> OnAlarmRaised;

    public bool AlarmRaised => alarmRaised;
    private bool alarmRaised;
    private int round;
    private bool open;

    private void Awake()
    {
        if (quizPanel != null) quizPanel.SetActive(false);
        if (option0 != null) option0.onClick.AddListener(() => Answer(0));
        if (option1 != null) option1.onClick.AddListener(() => Answer(1));
        if (option2 != null) option2.onClick.AddListener(() => Answer(2));
        if (option3 != null) option3.onClick.AddListener(() => Answer(3));
    }

    public void Interact(PlayerInteraction player)
    {
        if (alarmRaised || open) return;
        round = 0;
        open = true;
        ShowRound();
    }

    private void ShowRound()
    {
        var r = round == 0 ? round1 : round2;
        promptText.text = r.prompt;
        option0.GetComponentInChildren<Text>().text = r.optionA;
        option1.GetComponentInChildren<Text>().text = r.optionB;
        option2.GetComponentInChildren<Text>().text = r.optionC;
        option3.GetComponentInChildren<Text>().text = r.optionD;
        feedbackText.text = "";
        quizPanel.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Answer(int index)
    {
        var r = round == 0 ? round1 : round2;
        if (index == r.correctIndex)
        {
            if (round == 0)
            {
                round = 1;
                feedbackText.text = "✓ 正确。“地址讲清，消防车才能直达。”";
                // 短暂显示第一轮反馈后进第二轮（用真实时间，timeScale=0）
                StartCoroutine(NextRoundDelay(0.8f));
            }
            else
            {
                alarmRaised = true;
                feedbackText.text = "✓ 报警成功！" + doneHint;
                Time.timeScale = 1f;
                StartCoroutine(CloseDelay(2.2f));
                OnAlarmRaised?.Invoke(this);
            }
        }
        else
        {
            feedbackText.text = "✗ 不对。" + r.correction;
        }
    }

    private System.Collections.IEnumerator NextRoundDelay(float t)
    {
        float e = 0f;
        while (e < t) { e += Time.unscaledDeltaTime; yield return null; }
        ShowRound();
    }

    private System.Collections.IEnumerator CloseDelay(float t)
    {
        float e = 0f;
        while (e < t) { e += Time.unscaledDeltaTime; yield return null; }
        Hide();
    }

    private void Hide()
    {
        open = false;
        if (quizPanel != null) quizPanel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
