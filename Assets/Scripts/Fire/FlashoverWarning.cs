using UnityEngine;

/// <summary>
/// 轰燃前兆警告（M5公寓关）：与 FireSpreadManager 的时间表同步——
/// 在楼梯间火点点燃时刻（默认80秒），屏幕中央弹出轰燃警告并拉响紧急警报。
/// 轰燃（Flashover）是火灾最致命的突变：室内温度骤升，一切可燃物瞬间同时燃烧。
/// </summary>
public class FlashoverWarning : MonoBehaviour
{
    [Header("时机")]
    [Tooltip("轰燃时刻（秒），应与 FireSpreadManager 中楼梯火点的点燃时间一致")]
    [SerializeField] private float warnAtSeconds = 80f;

    [Header("警告文案（中央大字）")]
    [SerializeField, TextArea(2, 3)] private string warningText = "轰燃前兆！\n楼道即将被火焰吞没——立即向楼下撤离，不要停留！";

    [Header("引用")]
    [SerializeField] private TutorialUI tutorial;
    [SerializeField] private GameHUD hud;
    [SerializeField] private AudioSource sirenAudio;
    [SerializeField] private AudioClip sirenClip;

    private float timer;
    private bool fired;

    private void Update()
    {
        if (fired || Time.timeScale <= 0.01f) return;
        timer += Time.deltaTime;
        if (timer < warnAtSeconds) return;

        fired = true;

        // 紧急警报音
        if (sirenAudio != null && sirenClip != null)
        {
            sirenAudio.clip = sirenClip;
            sirenAudio.loop = true;
            sirenAudio.Play();
        }

        // 中央大字警告（复用TutorialUI提示通道，重复刷新加强感知）
        if (tutorial != null)
        {
            tutorial.ShowHint(warningText);
            tutorial.ShowHint(warningText); // 二连发延长显示
        }
        if (hud != null) hud.SetVignette(0.85f);
    }
}
