using UnityEngine;

/// <summary>
/// 公寓关119报警挂钩：玩家完成119话术 → 消防车警笛从场外消防车位置响起（报警的"反馈闭环"）
/// + TutorialUI 广播"消防队已接警"。Truck_Siren AudioSource 默认停在场景外(x=351)，报警后才归位播放。
/// 只挂在公寓场景（宿舍/办公楼不走119流程）；只触发"消防队响应"信号，
/// 不触发 AlarmButton 的全楼警铃（119电话与手动按钮在真实消防教学中是两个不同动作）。
/// </summary>
public class Apartment119Hooks : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private Phone119 phone119;
    [SerializeField] private AudioSource truckSiren;
    [SerializeField] private Transform fireTruck;
    [SerializeField] private TutorialUI tutorial;

    [Header("接警广播文案")]
    [SerializeField, TextArea(1, 2)] private string dispatchHint = "119已接警！消防队正在赶往现场——湿毛巾捂住口鼻，抓紧时间沿疏散方向撤离！";

    private bool alarmRaised;

    private void Awake()
    {
        if (phone119 == null) phone119 = FindFirstObjectByType<Phone119>();
        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialUI>();
        if (truckSiren == null)
        {
            var go = GameObject.Find("Truck_Siren");
            if (go != null) truckSiren = go.GetComponent<AudioSource>();
        }
        if (fireTruck == null)
        {
            var go = GameObject.Find("FireTruck");
            if (go != null) fireTruck = go.transform;
        }
    }

    private void OnEnable()
    {
        if (phone119 != null) phone119.OnAlarmRaised += OnAlarmRaised;
    }

    private void OnDisable()
    {
        if (phone119 != null) phone119.OnAlarmRaised -= OnAlarmRaised;
    }

    /// <summary>119报警完成（回调在 Phone119 的协程内触发，主线程）。</summary>
    private void OnAlarmRaised(Phone119 phone)
    {
        if (alarmRaised) return;
        alarmRaised = true;

        if (fireTruck != null && truckSiren != null)
            truckSiren.transform.position = fireTruck.transform.position;
        if (truckSiren != null && !truckSiren.isPlaying) truckSiren.Play();

        if (tutorial != null && !string.IsNullOrEmpty(dispatchHint)) tutorial.ShowHint(dispatchHint);
    }
}
