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
        Win
    }

    [Serializable]
    public class Step
    {
        public StepSource source;
        public string label;
        [TextArea(1, 2)] public string instruction;
        public Sprite icon;
        public Transform target;
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
                pathGuide.SetTarget(steps[index].target);
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

    private void OnPowerCut() => Complete(StepSource.PowerCut);
    private void OnExtinguisherTaken() => Complete(StepSource.Extinguisher);
    private void OnDeskFireOut(FirePoint fp) => Complete(StepSource.InitialFireOut);
    private void OnTowel() => Complete(StepSource.Towel);
    private void OnDoorOpened(InteractableDoor door) => Complete(StepSource.DoorOpen);
    private void OnBarrierFireOut(FirePoint fp) => Complete(StepSource.BarrierFireOut);
    private void OnWin() => Complete(StepSource.Win);
}
