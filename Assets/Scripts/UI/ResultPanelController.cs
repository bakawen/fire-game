using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>胜负结算面板：胜利/失败标题 + 详情 + 重试/返回关卡选择。</summary>
public class ResultPanelController : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button exitButton;

    private void Awake()
    {
        retryButton.onClick.AddListener(() => SceneLoader.Load(SceneManager.GetActiveScene().name));
        exitButton.onClick.AddListener(() => SceneLoader.Load(SceneNames.LevelSelect));
        Hide();
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
        panelRoot.SetActive(true);
        Time.timeScale = 0f;
        // 解锁鼠标供点击面板按钮（否则第一人称锁定状态下无法点击任何UI）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        Time.timeScale = 1f;
        panelRoot.SetActive(false);
    }
}
