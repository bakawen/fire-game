using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>全局游戏管理器：负责应用级状态与退出。</summary>
public class GameManager : MonoBehaviour
{
    private static GameManager _instance;

    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GameManager>();
                if (_instance == null)
                {
                    var go = new GameObject("GameManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<GameManager>();
                }
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        _ = Instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
#if UNITY_EDITOR
        // 编辑器中从任意场景按 Play 时先回到主菜单，保证完整流程体验（打包后首场景即主菜单，无需处理）
        if (SceneManager.GetActiveScene().name != SceneNames.MainMenu)
            SceneManager.LoadScene(SceneNames.MainMenu);
#endif
    }

    /// <summary>退出游戏（编辑器中停止运行，打包后退出应用）。</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
