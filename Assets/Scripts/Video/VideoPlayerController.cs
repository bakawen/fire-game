using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>科普视频播放控制：优先从 StreamingAssets 读取视频文件，播放完毕自动返回主菜单。</summary>
[RequireComponent(typeof(VideoPlayer))]
public class VideoPlayerController : MonoBehaviour
{
    private static readonly string[] VideoExtensions = { "*.mp4", "*.mov", "*.webm", "*.avi", "*.wmv" };

    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject placeholderRoot;

    private void Awake()
    {
        var audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        videoPlayer.renderMode = VideoRenderMode.CameraFarPlane;
        videoPlayer.targetCamera = Camera.main;
        videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, audioSource);
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.loopPointReached += _ => SceneLoader.Load(SceneNames.MainMenu);

        backButton.onClick.AddListener(() => SceneLoader.Load(SceneNames.MainMenu));
    }

    private void Start()
    {
        // 视频场景有返回按钮，必须释放鼠标（可能从锁定状态的场景切换而来）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        string videoPath = FindVideoFile();
        if (!string.IsNullOrEmpty(videoPath))
        {
            placeholderRoot.SetActive(false);
            videoPlayer.url = videoPath;
            videoPlayer.Play();
        }
        else
        {
            placeholderRoot.SetActive(true);
        }
    }

    private static string FindVideoFile()
    {
        string[] roots =
        {
            Path.Combine(Application.streamingAssetsPath, "Videos"),
            Application.streamingAssetsPath,
        };
        foreach (string root in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (string pattern in VideoExtensions)
            {
                string[] files = Directory.GetFiles(root, pattern);
                if (files.Length > 0) return files[0];
            }
        }
        return null;
    }
}
