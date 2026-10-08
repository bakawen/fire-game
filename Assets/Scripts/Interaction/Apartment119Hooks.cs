using UnityEngine;

/// <summary>
/// 公寓关119报警+B线固守待援挂钩。
/// 119线：完成话术 → 消防车警笛归位播放 + 接警广播。
/// B线：ShelterRescue 完成 → 固守待援结局结算面板。
/// 只挂在公寓场景。
/// </summary>
public class Apartment119Hooks : MonoBehaviour
{
    [Header("119报警")]
    [SerializeField] private Phone119 phone119;
    [SerializeField] private AudioSource truckSiren;
    [SerializeField] private Transform fireTruck;
    [SerializeField] private TutorialUI tutorial;
    [SerializeField, TextArea(1, 2)] private string dispatchHint = "119已接警！消防队正在赶往现场——湿毛巾捂住口鼻，抓紧时间沿疏散方向撤离！";

    [Header("B线固守待援")]
    [SerializeField] private ShelterRescue shelterRescue;
    [SerializeField] private ResultPanelController resultPanel;
    [SerializeField, TextArea(3, 8)] private string rescueWinText = "固守待援结局\n\n你成功堵住门缝、在窗口呼救，消防员及时赶到将你安全救出！\n\n固守待援要点：\n· 用湿毛巾或衣物堵住门缝，阻止浓烟进入\n· 到窗口挥动鲜艳衣物呼救\n· 保持冷静，不要贸然穿越浓烟\n· 关紧房门争取宝贵的等待时间";

    private bool alarmRaised;

    private void Awake()
    {
        if (phone119 == null) phone119 = FindFirstObjectByType<Phone119>();
        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialUI>();
        // Include：B_Line_Shelter 默认停用（B线触发才激活），不查 inactive 会漏引用
        if (shelterRescue == null) shelterRescue = FindFirstObjectByType<ShelterRescue>(FindObjectsInactive.Include);
        if (resultPanel == null) resultPanel = FindFirstObjectByType<ResultPanelController>();
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
        if (shelterRescue != null) shelterRescue.OnRescueComplete += OnRescueComplete;
    }

    private void OnDisable()
    {
        if (phone119 != null) phone119.OnAlarmRaised -= OnAlarmRaised;
        if (shelterRescue != null) shelterRescue.OnRescueComplete -= OnRescueComplete;
    }

    private void OnAlarmRaised(Phone119 phone)
    {
        if (alarmRaised) return;
        alarmRaised = true;
        if (fireTruck != null && truckSiren != null)
            truckSiren.transform.position = fireTruck.transform.position;
        if (truckSiren != null && !truckSiren.isPlaying) truckSiren.Play();
        if (tutorial != null && !string.IsNullOrEmpty(dispatchHint)) tutorial.ShowHint(dispatchHint);
    }

    private void OnRescueComplete(ShelterRescue rescue)
    {
        if (resultPanel == null) return;
        resultPanel.ShowWin(rescueWinText);
    }
}
