using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 图标化关卡引导（实验性，宿舍关先行）：每步弹出中央 UI 弹窗（大图标+步骤名+做法说明）+ 当前目标 3D 浮动箭头。
/// 步骤按顺序配置（事件源+说明+图标+目标物），任一事件完成后推进到第一个未完成步骤（乱序完成自动跳过）。
/// 节奏：步骤完成→绿勾提示（1.4s）→下一步弹窗（延迟 1.5s）；首步弹窗延迟等开场演出。
/// 教学设计：操作引导全部图标化弹窗，文字仅保留开场剧情与知识点反馈（TutorialUI）。
/// </summary>
public class GuidanceChecklist : MonoBehaviour
{
    /// <summary>步骤完成事件源；Proximity=玩家进入目标半径即完成（如低姿穿越火障走廊）。</summary>
    public enum StepSource
    {
        PowerCut,
        Extinguisher,
        InitialFireOut,
        Towel,
        DoorOpen,
        BarrierFireOut,
        Proximity,
        Win,
        Alarm,      // 手动报警按钮（办公楼/公寓）
        Cabinet,    // 打碎消防柜取灭火器（办公楼）
        DoorCheck   // 摸门判断火情（公寓）
    }

    [Serializable]
    public class Step
    {
        public StepSource source;
        public string label;
        [TextArea(1, 2)] public string instruction;
        public Sprite icon;
        public Transform target;
        [Tooltip("地面箭头的途经点链（办公楼安全路线引导）；空=最短路径")]
        public Transform[] viaPoints;
        public float arrowHeight = 1.6f;
        public float radius = 3.5f;
        [NonSerialized] public bool done;
    }

    [Header("引用")]
    [SerializeField] private GameHUD hud;
    [SerializeField] private Transform player;
    [SerializeField] private bool hideStaticObjective = true;

    [Header("事件源")]
    [SerializeField] private ElectricalPanel electricalPanel;
    [SerializeField] private FireExtinguisher extinguisher;
    [SerializeField] private FirePoint deskFire;
    [SerializeField] private WetTowel wetTowel;
    [SerializeField] private InteractableDoor roomDoor;
    [SerializeField] private FirePoint barrierFire;
    [SerializeField] private WinTrigger winTrigger;
    [SerializeField] private AlarmButton alarmButton;        // 办公楼/公寓：手动报警
    [SerializeField] private ExtinguisherCabinet cabinet;    // 办公楼：消防柜取灭火器
    [SerializeField] private DoorCheck doorCheck;            // 公寓：摸门判断火情

    [Header("步骤列表（按顺序配置；乱序完成自动跳过）")]
    [SerializeField] private List<Step> steps = new List<Step>();

    [Header("UI 资源")]
    [SerializeField] private Sprite checkIcon;
    [SerializeField] private Sprite arrowIcon;
    [SerializeField] private Texture2D groundArrowTexture;   // 地面疏散箭头贴图
    [SerializeField] private float firstPopupDelay = 3f;   // 首步弹窗等开场演出（黑幕淡出）

    private ObjectivePopupUI popup;
    private ObjectiveGuideArrow arrow;
    private PathGuide pathGuide;
    private Transform[] routeChain;          // 路线触发器注入（办公楼活火场：玩家选定路线后的箭头途经链）
    private int shownIndex = -2;           // 已弹过窗的当前步骤
    private float nextPopupDelay;          // 下一次弹窗前的等待（首步=开场延迟；之后=完成提示时长）
    private Coroutine popupRoutine;

    private void Awake()
    {
        if (player == null)
        {
            var pi = FindFirstObjectByType<PlayerInteraction>();
            if (pi != null) player = pi.transform;
        }
        if (hud != null && hideStaticObjective) hud.SetObjectiveVisible(false);

        popup = ObjectivePopupUI.Create(checkIcon);
        if (arrowIcon != null) arrow = ObjectiveGuideArrow.Create(arrowIcon);
        pathGuide = PathGuide.Create(player, groundArrowTexture);

        nextPopupDelay = firstPopupDelay;
        Refresh();
    }

    private void OnEnable()
    {
        if (electricalPanel != null) electricalPanel.OnPowerCut += OnPowerCut;
        if (extinguisher != null) extinguisher.OnPickedUp += OnExtinguisherTaken;
        if (deskFire != null) deskFire.OnExtinguished += OnDeskFireOut;
        if (wetTowel != null) wetTowel.OnPickedUp += OnTowel;
        if (roomDoor != null) roomDoor.OnOpened += OnDoorOpened;
        if (barrierFire != null) barrierFire.OnExtinguished += OnBarrierFireOut;
        if (winTrigger != null) winTrigger.OnWin += OnWin;
        if (alarmButton != null) alarmButton.OnAlarmTriggered += OnAlarm;
        if (cabinet != null) cabinet.OnTaken += OnCabinetTaken;
        if (doorCheck != null) doorCheck.OnDoorChecked += OnDoorChecked;
    }

    private void OnDisable()
    {
        if (electricalPanel != null) electricalPanel.OnPowerCut -= OnPowerCut;
        if (extinguisher != null) extinguisher.OnPickedUp -= OnExtinguisherTaken;
        if (deskFire != null) deskFire.OnExtinguished -= OnDeskFireOut;
        if (wetTowel != null) wetTowel.OnPickedUp -= OnTowel;
        if (roomDoor != null) roomDoor.OnOpened -= OnDoorOpened;
        if (barrierFire != null) barrierFire.OnExtinguished -= OnBarrierFireOut;
        if (winTrigger != null) winTrigger.OnWin -= OnWin;
        if (alarmButton != null) alarmButton.OnAlarmTriggered -= OnAlarm;
        if (cabinet != null) cabinet.OnTaken -= OnCabinetTaken;
        if (doorCheck != null) doorCheck.OnDoorChecked -= OnDoorChecked;
    }

    private void Update()
    {
        // Proximity 步骤：当前目标进入半径即完成（用于"穿越火障"等无法用事件表达的位置类步骤）
        int index = FirstUndone();
        if (index < 0 || player == null) return;
        var s = steps[index];
        if (s.source != StepSource.Proximity || s.target == null) return;
        if (Vector3.Distance(player.position, s.target.position) <= s.radius)
            Complete(StepSource.Proximity);
    }

    private void Complete(StepSource src)
    {
        foreach (var s in steps)
        {
            if (s.source == src && !s.done)
            {
                s.done = true;
                if (popup != null) popup.ShowDone(checkIcon, s.label);   // 完成绿勾提示
                nextPopupDelay = 1.5f;                                    // 绿勾后再弹下一步
                break;
            }
        }
        Refresh();
    }

    /// <summary>路线跟随（办公楼活火场）：RouteTrigger 在玩家选定路线后注入该路线的地面箭头链。</summary>
    public void SetRouteChain(Transform[] chain)
    {
        routeChain = chain;
        Refresh();
    }

    private int FirstUndone()
    {
        for (int i = 0; i < steps.Count; i++)
            if (!steps[i].done) return i;
        return -1;
    }

    /// <summary>推进到第一个未完成步骤：移动引导箭头+路径线；当前步骤变化时弹出步骤说明。</summary>
    private void Refresh()
    {
        int index = FirstUndone();

        if (popup != null && index != shownIndex)
        {
            shownIndex = index;
            if (popupRoutine != null) StopCoroutine(popupRoutine);
            if (index >= 0) popupRoutine = StartCoroutine(PopupRoutine(index, nextPopupDelay));
            else popup.HideNow();
            nextPopupDelay = 1.5f;
        }

        if (pathGuide != null)
        {
            if (index >= 0 && steps[index].target != null)
                pathGuide.SetTarget(steps[index].target, EffectiveVia(steps[index]));
            else
                pathGuide.Hide();
        }

        if (arrow == null) return;
        if (index >= 0 && steps[index].target != null)
            arrow.SetTarget(steps[index].target, steps[index].arrowHeight);
        else
            arrow.Hide();
    }

    private IEnumerator PopupRoutine(int index, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        var s = steps[index];
        popup.ShowStep(s.icon, $"第 {index + 1} 步 · {s.label}", s.instruction);
    }

    /// <summary>步骤自带途经点优先；全空时回退路线触发器注入的路线链（活火场版）。</summary>
    private Transform[] EffectiveVia(Step s)
    {
        if (s.viaPoints != null)
        {
            for (int i = 0; i < s.viaPoints.Length; i++)
                if (s.viaPoints[i] != null) return s.viaPoints;
        }
        return routeChain;
    }

    private void OnPowerCut() => Complete(StepSource.PowerCut);
    private void OnExtinguisherTaken() => Complete(StepSource.Extinguisher);
    private void OnDeskFireOut(FirePoint fp) => Complete(StepSource.InitialFireOut);
    private void OnTowel() => Complete(StepSource.Towel);
    private void OnDoorOpened(InteractableDoor door) => Complete(StepSource.DoorOpen);
    private void OnBarrierFireOut(FirePoint fp) => Complete(StepSource.BarrierFireOut);
    private void OnWin() => Complete(StepSource.Win);
    private void OnAlarm() => Complete(StepSource.Alarm);
    private void OnCabinetTaken() => Complete(StepSource.Cabinet);
    private void OnDoorChecked() => Complete(StepSource.DoorCheck);
}
