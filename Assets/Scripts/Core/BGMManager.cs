using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>跨场景背景音乐管理器：主菜单/关卡选择播菜单曲，科普/答题播学习曲，游戏关卡静音；
/// 同曲目场景间切换不重播（无缝延续），异曲目切换用交叉淡入淡出。四个菜单场景各放一个副本，运行时单例去重。</summary>
public class BGMManager : MonoBehaviour
{
    [SerializeField] private AudioClip menuBgm;   // 主菜单 + 关卡选择
    [SerializeField] private AudioClip learnBgm;  // 科普 + 答题
    [SerializeField, Range(0f, 1f)] private float volume = 0.45f;
    [SerializeField] private float crossfadeSeconds = 0.8f;

    private static BGMManager _instance;
    private AudioSource _current;
    private AudioSource _fading;
    private AudioClip _targetClip;
    private Coroutine _fadeRoutine;
    private bool _duplicate;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            _duplicate = true;
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        _current = gameObject.AddComponent<AudioSource>();
        _fading = gameObject.AddComponent<AudioSource>();
        foreach (AudioSource source in new[] { _current, _fading })
        {
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
        }
    }

    private void OnEnable()
    {
        if (_duplicate) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplySceneMusic();
    }

    private void OnDisable()
    {
        if (_duplicate) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneMusic();
    }

    /// <summary>按当前场景名决定目标曲目；与当前目标相同则不做任何事，保证同曲目场景间无缝延续。</summary>
    private void ApplySceneMusic()
    {
        string scene = SceneManager.GetActiveScene().name;
        AudioClip target =
            scene == SceneNames.MainMenu || scene == SceneNames.LevelSelect ? menuBgm :
            scene == SceneNames.FireScience || scene == SceneNames.Quiz ? learnBgm : null;
        if (target == _targetClip) return;
        _targetClip = target;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(Crossfade(target));
    }

    private IEnumerator Crossfade(AudioClip target)
    {
        AudioSource outgoing = _current;
        AudioSource incoming = _fading;
        _current = incoming;
        _fading = outgoing;

        incoming.clip = target;
        incoming.volume = 0f;
        if (target != null) incoming.Play();

        float outgoingStart = outgoing.isPlaying ? outgoing.volume : 0f;
        float elapsed = 0f;
        while (elapsed < crossfadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, crossfadeSeconds));
            outgoing.volume = Mathf.Lerp(outgoingStart, 0f, t);
            incoming.volume = target != null ? Mathf.Lerp(0f, volume, t) : 0f;
            yield return null;
        }
        outgoing.Stop();
        outgoing.volume = 0f;
    }
}
