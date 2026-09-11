using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>知识问答门（B4）：回答消防选择题才能开启的门，答错短暂锁闭。</summary>
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
        public int correctIndex; // 0=A 1=B 2=C
        [TextArea(1, 2)] public string explanation;
    }

    [Header("题目")]
    [SerializeField] private QuizQuestion[] questions;

    [Header("UI引用")]
    [SerializeField] private GameObject quizPanel;
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private Button optionAButton;
    [SerializeField] private Button optionBButton;
    [SerializeField] private Button optionCButton;
    [SerializeField] private TMP_Text feedbackText;

    [Header("行为")]
    [SerializeField] private float wrongLockSeconds = 5f;

    private InteractableDoor door;
    private QuizQuestion current;
    private bool answered;

    private void Awake()
    {
        door = GetComponent<InteractableDoor>();
        // 开门拦截：未答完题时按E先弹问答
        door.OpenInterceptor = p =>
        {
            if (ShouldAsk()) { Ask(); return true; }
            return false;
        };
        BindButtons();
        Hide();
    }

    private bool ShouldAsk()
    {
        return questions != null && questions.Length > 0 && !door.IsOpen && !answered;
    }

    /// <summary>玩家尝试开门时被拦截：先答题（由门事件驱动，本关由 LevelFlow 调用）。</summary>
    public void Ask()
    {
        if (!ShouldAsk()) return;
        current = questions[UnityEngine.Random.Range(0, questions.Length)];
        questionText.text = current.question;
        optionAButton.GetComponentInChildren<TMP_Text>().text = current.optionA;
        optionBButton.GetComponentInChildren<TMP_Text>().text = current.optionB;
        optionCButton.GetComponentInChildren<TMP_Text>().text = current.optionC;
        feedbackText.text = "";
        quizPanel.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Answer(int index)
    {
        if (current == null) return;
        if (index == current.correctIndex)
        {
            answered = true;
            feedbackText.text = "回答正确！" + current.explanation;
            // timeScale=0 时 Invoke 不计时：先恢复时间再开门
            Time.timeScale = 1f;
            Invoke(nameof(CloseAndOpen), 1.2f);
        }
        else
        {
            feedbackText.text = "回答错误，门暂时锁闭 " + wrongLockSeconds + " 秒后可重试。" + current.explanation;
            // 惩罚计时用真实时间（unscaled，不受timeScale=0影响）
            StartCoroutine(RetryRoutine());
        }
    }

    private System.Collections.IEnumerator RetryRoutine()
    {
        float t = 0f;
        while (t < wrongLockSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Hide();
    }

    private void CloseAndOpen()
    {
        Hide();
        // 正确后由外部直接开门
        if (!door.IsOpen)
        {
            var player = FindObjectOfType<PlayerInteraction>();
            door.Interact(player);
        }
    }

    private void Hide()
    {
        if (quizPanel != null) quizPanel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Inspector 接线用
    public void BindButtons()
    {
        optionAButton.onClick.AddListener(() => Answer(0));
        optionBButton.onClick.AddListener(() => Answer(1));
        optionCButton.onClick.AddListener(() => Answer(2));
    }
}
