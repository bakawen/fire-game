using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>场景加载器：黑屏淡入淡出 + 异步加载。通过 SceneLoader.Load(场景名) 使用。</summary>
public class SceneLoader : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 0.35f;

    private static SceneLoader _instance;
    private Image _fadeImage;
    private bool _loading;

    public static SceneLoader Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<SceneLoader>();
                if (_instance == null)
                {
                    var go = new GameObject("SceneLoader");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<SceneLoader>();
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
        BuildFadeCanvas();
    }

    /// <summary>带黑屏过渡加载场景。</summary>
    public static void Load(string sceneName)
    {
        if (Instance._loading) return;
        Instance.StartCoroutine(Instance.LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        _loading = true;
        Time.timeScale = 1f;
        yield return Fade(0f, 1f);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        while (operation != null && !operation.isDone)
            yield return null;

        yield return Fade(1f, 0f);
        _loading = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (_fadeImage == null) yield break;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
            _fadeImage.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }
        _fadeImage.color = new Color(0f, 0f, 0f, to);
    }

    private void BuildFadeCanvas()
    {
        var canvasGo = new GameObject("FadeCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _fadeImage = canvasGo.AddComponent<Image>();
        _fadeImage.color = new Color(0f, 0f, 0f, 0f);
        _fadeImage.raycastTarget = false;
    }
}
