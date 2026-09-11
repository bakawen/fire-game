using UnityEngine;
using UnityEngine.UI;

/// <summary>主菜单界面：火灾科普 / 游玩游戏 / 退出。</summary>
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button fireScienceButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        fireScienceButton.onClick.AddListener(() => SceneLoader.Load(SceneNames.Video));
        playButton.onClick.AddListener(() => SceneLoader.Load(SceneNames.LevelSelect));
        quitButton.onClick.AddListener(GameManager.Instance.QuitGame);
    }

    private void Start()
    {
        // 主菜单必须释放鼠标（从关卡场景重定向回来时鼠标可能仍处于锁定隐藏状态）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
