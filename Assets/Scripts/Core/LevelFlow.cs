using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡流程编排器（数据驱动）：阶段列表在场景内配置（事件源+目标文案+完成提示），
/// 任一事件完成后自动推进到第一个未完成阶段（乱序完成自动跳过）。
/// 每关一个实例，03_Dorm 不使用本组件。
/// </summary>
public class LevelFlow : MonoBehaviour
{
    /// <summary>阶段完成事件源；None 表示不由事件完成（最终撤离目标）。</summary>
    public enum StageSource
    {
        None,
        Alarm,
        PowerCut,
        Cabinet,
        InitialFireOut,
        Towel,
        QuizGate,
        DoorCheck
    }

    [Serializable]
    public class Stage
    {
        public StageSource source;
        [TextArea(1, 2)] public string objective;
        [TextArea(1, 3)] public string hint = "";
        [NonSerialized] public bool done;
    }

    [Header("引用")]
    [SerializeField] private TutorialUI tutorial;
    [SerializeField] private GameHUD hud;

    [Header("事件源")]
    [SerializeField] private AlarmButton alarmButton;
    [SerializeField] private ElectricalPanel electricalPanel;
    [SerializeField] private ExtinguisherCabinet extinguisherCabinet;
    [SerializeField] private FirePoint initialFire;
    [SerializeField] private WetTowel wetTowel;
    [SerializeField] private CrawlZone crawlZone;
    [SerializeField] private ElevatorTrap elevatorTrap;
    [SerializeField] private InteractableDoor quizGate;
    [SerializeField] private DoorCheck doorCheck;

    [Header("阶段列表（按顺序配置；乱序完成自动跳过）")]
    [SerializeField] private List<Stage> stages = new List<Stage>();

    [Header("非阶段事件提示（触发显示，不推进目标）")]
    [SerializeField, TextArea(1, 3)] private string crawlHint = "";
    [SerializeField, TextArea(1, 3)] private string elevatorHint = "";

    private void Awake()
    {
        RefreshObjective();
    }

    private void OnEnable()
    {
        if (alarmButton != null) alarmButton.OnAlarmTriggered += OnAlarm;
        if (electricalPanel != null) electricalPanel.OnPowerCut += OnPowerCut;
        if (extinguisherCabinet != null) extinguisherCabinet.OnTaken += OnCabinet;
        if (initialFire != null) initialFire.OnExtinguished += OnInitialFireOut;
        if (wetTowel != null) wetTowel.OnPickedUp += OnTowel;
        if (quizGate != null) quizGate.OnOpened += OnQuizGate;
        if (crawlZone != null) crawlZone.OnStandingDamage += OnCrawl;
        if (elevatorTrap != null) elevatorTrap.OnTriedElevator += OnElevator;
        if (doorCheck != null) doorCheck.OnDoorChecked += OnDoorChecked;
    }

    private void OnDisable()
    {
        if (alarmButton != null) alarmButton.OnAlarmTriggered -= OnAlarm;
        if (electricalPanel != null) electricalPanel.OnPowerCut -= OnPowerCut;
        if (extinguisherCabinet != null) extinguisherCabinet.OnTaken -= OnCabinet;
        if (initialFire != null) initialFire.OnExtinguished -= OnInitialFireOut;
        if (wetTowel != null) wetTowel.OnPickedUp -= OnTowel;
        if (quizGate != null) quizGate.OnOpened -= OnQuizGate;
        if (crawlZone != null) crawlZone.OnStandingDamage -= OnCrawl;
        if (elevatorTrap != null) elevatorTrap.OnTriedElevator -= OnElevator;
        if (doorCheck != null) doorCheck.OnDoorChecked -= OnDoorChecked;
    }

    /// <summary>完成第一个匹配该事件源的未完成阶段并推进目标；返回被完成的阶段（无则null）。</summary>
    private Stage Complete(StageSource src)
    {
        Stage hit = null;
        foreach (var s in stages)
        {
            if (s.source == src && !s.done)
            {
                s.done = true;
                hit = s;
                break;
            }
        }
        RefreshObjective();
        return hit;
    }

    /// <summary>显示第一个未完成阶段的目标文案。</summary>
    private void RefreshObjective()
    {
        if (hud == null || stages == null || stages.Count == 0) return;
        foreach (var s in stages)
        {
            if (!s.done)
            {
                hud.SetObjective(s.objective);
                return;
            }
        }
    }

    private void ShowHint(string text)
    {
        if (tutorial != null && !string.IsNullOrEmpty(text)) tutorial.ShowHint(text);
    }

    private void OnAlarm() { ShowHint(Complete(StageSource.Alarm)?.hint); }
    private void OnPowerCut() { ShowHint(Complete(StageSource.PowerCut)?.hint); }
    private void OnCabinet() { ShowHint(Complete(StageSource.Cabinet)?.hint); }
    private void OnInitialFireOut(FirePoint fp) { ShowHint(Complete(StageSource.InitialFireOut)?.hint); }
    private void OnTowel() { ShowHint(Complete(StageSource.Towel)?.hint); }
    private void OnQuizGate(InteractableDoor door) { ShowHint(Complete(StageSource.QuizGate)?.hint); }
    private void OnDoorChecked()
    {
        var hit = Complete(StageSource.DoorCheck);
        // 摸门结果的温度判定文案由 DoorCheck 提供（含分支教学）
        if (hit != null && doorCheck != null) ShowHint(doorCheck.GetResultHint());
        else if (hit != null) ShowHint(hit.hint);
    }

    private void OnCrawl() { ShowHint(crawlHint); }
    private void OnElevator() { ShowHint(elevatorHint); }
}
