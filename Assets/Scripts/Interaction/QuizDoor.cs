using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 学习型问答门（活火场版）：开门前展示一道消防选择题——答对直接开门；
/// 答错不锁门，显示正确答案与解释后同样放行（去掉旧版的锁门惩罚——答题本身即学习）。
/// 沿用原问答门的序列化题库与 UI 引用，场景数据无需改动。仅办公楼场景使用。
/// </summary>
[RequireComponent(typeof(InteractableDoor))]
public class QuizDoor : MonoBehaviour
{
    [System.Serializable]
    public class QuizQuestion
    {
        [TextArea(2, 3)] public string question;
        [TextArea(1, 2)] public string optionA;
        [TextArea(1, 2)] public string optionB;
        [TextArea(1, 2)] public string optionC;
        public int correctIndex;      // 0=A 1=B 2=C
        [TextArea(2, 3)] public string explanation;
    }

    [Header("题库")]
    [SerializeField] private QuizQuestion[] questions;

    [Header("UI 引用")]
    [SerializeField] private GameObject quizPanel;
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private Button optionAButton;
    [SerializeField] private Button optionBButton;
    [SerializeField] private Button optionCButton;
    [SerializeField] private TMP_Text feedbackText;

    [Header("文案")]
    [SerializeField] private string correctPrefix = "回答正确！";
    [SerializeField] private string wrongPrefix = "回答错误。正确答案是";

    private InteractableDoor door;
    private QuizQuestion current;
    private bool answered;

    private void Awake()
    {
        door = GetComponent<InteractableDoor>();
        door.OpenInterceptor = p =>
        {
            if (ShouldAsk()) { Ask(); return true; }
            return false;
        };
        optionAButton.onClick.AddListener(() => Answer(0));
        optionBButton.onClick.AddListener(() => Answer(1));
        optionCButton.onClick.AddListener(() => Answer(2));
    }

    private bool ShouldAsk()
    {
        return questions != null && questions.Length > 0 && !door.IsOpen && !answered;
    }

    /// <summary>玩家尝试开门被拦截：展示题目与选项。</summary>
    public void Ask()
    {
        if (!ShouldAsk()) return;
        current = questions[UnityEngine.Random.Range(0, questions.Length)];
        questionText.text = current.question;
        optionAButton.GetComponentInChildren<TMP_Text>().text = current.optionA;
        optionBButton.GetComponentInChildren<TMP_Text>().text = current.optionB;
        optionCButton.GetComponentInChildren<TMP_Text>().text = current.optionC;
        optionAButton.gameObject.SetActive(true);
        optionBButton.gameObject.SetActive(true);
        optionCButton.gameObject.SetActive(true);
        feedbackText.text = "";
        quizPanel.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Answer(int index)
    {
        if (current == null || answered) return;
        answered = true;   // 首次作答即完成学习，无论对错都不再弹第二次

        char letter = index == 0 ? 'A' : index == 1 ? 'B' : 'C';
        bool correct = index == current.correctIndex;
        if (correct)
        {
            feedbackText.text = $"<color=#6FDE78>{correctPrefix}</color>{current.explanation}";
        }
        else
        {
            string correctLetter = current.correctIndex == 0 ? "A" : current.correctIndex == 1 ? "B" : "C";
            string correctText = current.correctIndex == 0 ? current.optionA : current.correctIndex == 1 ? current.optionB : current.optionC;
            feedbackText.text = $"<color=#F25940>{wrongPrefix} {correctLetter}：{correctText}</color>\n{current.explanation}";
        }

        // 高亮正确按钮（绿框提示），答错的选项不再响应
        SetButtonsInteractable(false);

        // 学习完成即放行：答对 1.2s、答错多留 1s 读正确答案
        Time.timeScale = 1f;
        Invoke(nameof(OpenAndHide), correct ? 1.2f : 2.2f);
    }

    private void SetButtonsInteractable(bool on)
    {
        optionAButton.interactable = on;
        optionBButton.interactable = on;
        optionCButton.interactable = on;
    }

    private void OpenAndHide()
    {
        if (quizPanel != null) quizPanel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (!door.IsOpen)
        {
            var player = FindFirstObjectByType<PlayerInteraction>();
            door.Interact(player);
        }
    }
}
