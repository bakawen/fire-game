using System.Collections;
using UnityEngine;

/// <summary>
/// 计时倒塌物（M5公寓关）：随火灾阶段在指定时刻倒塌，封锁/改变撤离路线。
/// 用途：4F卧室衣柜（倒向门口制造心理压迫）、走廊书架（倒向楼梯口逼玩家绕行）。
/// 倒塌为Transform旋转动画（含碰撞体一起旋转），倒地后保持可被跨过或绕行。
/// </summary>
public class CollapseTimed : MonoBehaviour
{
    [Header("时机")]
    [Tooltip("倒塌时刻（秒），与 FireSpreadManager 阶段时间对齐")]
    [SerializeField] private float collapseAtSeconds = 45f;

    [Header("倒塌方向")]
    [Tooltip("倒塌旋转轴（局部空间）：如衣柜向前倒 = X轴90°")]
    [SerializeField] private Vector3 collapseEuler = new Vector3(90f, 0f, 0f);
    [SerializeField] private float collapseDuration = 0.6f;

    [Header("音效")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rumbleClip;

    [Header("警告")]
    [Tooltip("倒塌前提示（可选）")]
    [SerializeField, TextArea(1, 2)] private string preCollapseHint = "";
    [SerializeField] private float hintAdvanceSeconds = 3f;
    [SerializeField] private TutorialUI tutorial;

    private float timer;
    private bool done;

    private void Update()
    {
        if (done || Time.timeScale <= 0.01f) return;
        timer += Time.deltaTime;

        if (!string.IsNullOrEmpty(preCollapseHint) && tutorial != null &&
            timer >= collapseAtSeconds - hintAdvanceSeconds && timer < collapseAtSeconds)
        {
            // 仅显示一次（用倒塌标志位前的窗口判断）
        }

        if (timer >= collapseAtSeconds)
        {
            done = true;
            StartCoroutine(DoCollapse());
        }
    }

    private IEnumerator DoCollapse()
    {
        if (tutorial != null && !string.IsNullOrEmpty(preCollapseHint))
            tutorial.ShowHint(preCollapseHint);

        if (audioSource != null && rumbleClip != null)
            audioSource.PlayOneShot(rumbleClip);

        Quaternion start = transform.rotation;
        Quaternion end = Quaternion.Euler(collapseEuler) * start;
        float t = 0f;
        while (t < collapseDuration)
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(start, end, Mathf.Clamp01(t / collapseDuration));
            yield return null;
        }
        transform.rotation = end;
    }
}
