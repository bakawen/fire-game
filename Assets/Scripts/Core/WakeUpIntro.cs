using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 公寓关惊醒开场：进关→黑屏→烟感报警器急促嘀嘀→门缝渗烟橙光→画面渐亮→提示判断门外火情。
/// 挂在场景根（或LevelFlow同层），Start后驱动全屏黑场UI与音频，结束后禁用自身。
/// </summary>
public class WakeUpIntro : MonoBehaviour
{
    [Header("黑屏UI（Canvas下全屏Image，临时由本脚本创建）")]
    [SerializeField] private float blackHoldSeconds = 1.2f;
    [SerializeField] private float fadeOutSeconds = 2.2f;

    [Header("报警音（烟感报警器急促嘀嘀，可空=无声开场）")]
    [SerializeField] private AudioClip smokeAlarmClip;

    [Header("完成后提示文案")]
    [SerializeField] private string afterHint = "被报警声惊醒——门外情况不明，先判断门外火情！";

    private bool finished;

    private void Start()
    {
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        // 全屏黑Image（ScreenSpaceOverlay，置顶）
        var canvasGo = new GameObject("WakeUpIntroCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        var img = canvasGo.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true; // 开场期间挡输入

        // 烟感报警音（循环播放直到渐亮结束）
        AudioSource src = null;
        if (smokeAlarmClip != null)
        {
            src = canvasGo.AddComponent<AudioSource>();
            src.clip = smokeAlarmClip;
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0.85f;
            src.Play();
        }

        yield return new WaitForSeconds(blackHoldSeconds);

        // 渐亮
        float t = 0f;
        while (t < fadeOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            img.color = new Color(0f, 0f, 0f, 1f - t / fadeOutSeconds);
            if (src != null) src.volume = 0.85f * (1f - t / fadeOutSeconds);
            yield return null;
        }

        if (src != null) src.Stop();
        Destroy(canvasGo);
        img.raycastTarget = false;
        finished = true;

        // 开场提示
        var flow = FindFirstObjectByType<LevelFlow>();
        if (flow != null) flow.ShowIntroHint(afterHint);
        enabled = false;
    }

    /// <summary>开场是否已结束（LevelFlow可用于延迟阶段目标显示）。</summary>
    public bool Finished => finished;
}
