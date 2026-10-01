using System.Text;
using UnityEngine;

/// <summary>
/// 办公楼决策记录挂钩（活火场版）：订阅各决策事件 → DecisionLedger 时间轴（带火场快照）+ 楼宇系统广播。
/// 防火门的烟流联动由 FireZoneGraph 自己处理（门=阀门）；本组件只负责记账、广播、死亡验尸。
/// 只挂在办公楼场景；宿舍/公寓无本组件、账本无记录。
/// </summary>
public class OfficeDecisionHooks : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private AlarmButton alarmButton;
    [SerializeField] private ElectricalPanel electricalPanel;   // 断电广播（记账由 PowerCutEffects 负责）
    [SerializeField] private InteractableDoor quizGate;
    [Tooltip("要记账的边界防火门（A线入口门/走廊口门等），问答门单独处理不重复登记")]
    [SerializeField] private InteractableDoor[] trackedDoors;
    [SerializeField] private FireZoneGraph graph;
    [SerializeField] private FireExtinguisher extinguisher;
    [SerializeField] private TutorialUI tutorial;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("系统广播文案（TutorialUI 提示通道，替代已删除的手机群聊）")]
    [SerializeField, TextArea(1, 2)] private string alarmBroadcast = "消防控制室：手动报警已确认——警铃联动全楼，请立即沿疏散指示撤离！";
    [SerializeField, TextArea(1, 2)] private string powerCutBroadcast = "区域配电已切断——电气火失去电源，应急照明启动。";
    [SerializeField, TextArea(1, 2)] private string rootedBroadcast = "灭火器压制 + 断电——电气火已被根除，火场开始收缩。";
    [SerializeField, TextArea(1, 2)] private string reigniteBroadcast = "电弧复燃！不断电，电气火灾无法根除——立即撤离！";

    private void Awake()
    {
        DecisionLedger.Reset();   // 重玩本关时清空上一局记录

        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialUI>();
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null) playerHealth.DeathSummaryProvider = BuildAutopsy;
    }

    private void OnEnable()
    {
        if (alarmButton != null) alarmButton.OnAlarmTriggered += OnAlarm;
        if (electricalPanel != null) electricalPanel.OnPowerCut += OnPowerCut;
        if (extinguisher != null) extinguisher.OnPickedUp += OnExtinguisherPicked;
        if (quizGate != null)
        {
            quizGate.OnOpened += OnQuizGateOpened;
            quizGate.OnClosed += OnQuizGateClosed;
        }
        if (trackedDoors != null)
            foreach (var d in trackedDoors)
            {
                if (d == null) continue;
                d.OnOpened += OnTrackedDoorOpened;
                d.OnClosed += OnTrackedDoorClosed;
            }
        if (graph != null)
        {
            graph.OnOriginSelected += OnOriginSelected;
            graph.OnOriginSuppressed += OnOriginSuppressed;
            graph.OnOriginRooted += OnOriginRooted;
            graph.OnOriginReignited += OnOriginReignited;
            graph.OnZoneCritical += OnZoneCritical;
            graph.OnZoneSmoky += OnZoneSmoky;
        }
    }

    private void OnDisable()
    {
        if (alarmButton != null) alarmButton.OnAlarmTriggered -= OnAlarm;
        if (electricalPanel != null) electricalPanel.OnPowerCut -= OnPowerCut;
        if (extinguisher != null) extinguisher.OnPickedUp -= OnExtinguisherPicked;
        if (quizGate != null)
        {
            quizGate.OnOpened -= OnQuizGateOpened;
            quizGate.OnClosed -= OnQuizGateClosed;
        }
        if (trackedDoors != null)
            foreach (var d in trackedDoors)
            {
                if (d == null) continue;
                d.OnOpened -= OnTrackedDoorOpened;
                d.OnClosed -= OnTrackedDoorClosed;
            }
        if (graph != null)
        {
            graph.OnOriginSelected -= OnOriginSelected;
            graph.OnOriginSuppressed -= OnOriginSuppressed;
            graph.OnOriginRooted -= OnOriginRooted;
            graph.OnOriginReignited -= OnOriginReignited;
            graph.OnZoneCritical -= OnZoneCritical;
            graph.OnZoneSmoky -= OnZoneSmoky;
        }
    }

    // ————— 决策事件 —————

    private void OnAlarm()
    {
        DecisionLedger.RecordAlarm(Snapshot());
        Broadcast(alarmBroadcast);
    }

    private void OnPowerCut()
    {
        // 记账由 PowerCutEffects.Apply 统一负责（幂等），这里补广播
        Broadcast(powerCutBroadcast);
    }

    private void OnExtinguisherPicked() => DecisionLedger.RecordExtinguisherPicked();

    private void OnQuizGateOpened(InteractableDoor door) => DecisionLedger.RecordQuizGatePassed(Snapshot());
    private void OnQuizGateClosed(InteractableDoor door) => DecisionLedger.RecordDoorClosed("问答防火门", Snapshot());

    private void OnTrackedDoorOpened(InteractableDoor door)
        => DecisionLedger.RecordDoorOpened(door.gameObject.name, Snapshot());
    private void OnTrackedDoorClosed(InteractableDoor door)
        => DecisionLedger.RecordDoorClosed(door.gameObject.name, Snapshot());

    // ————— 活火场事件 —————

    private void OnOriginSelected() => DecisionLedger.RecordOriginSelected(graph.ActiveOriginLabel);
    private void OnOriginSuppressed() => DecisionLedger.RecordOriginSuppressed(Snapshot());
    private void OnOriginRooted()
    {
        DecisionLedger.RecordOriginRooted(Snapshot());
        Broadcast(rootedBroadcast);
    }
    private void OnOriginReignited()
    {
        DecisionLedger.RecordOriginReignited(Snapshot());
        Broadcast(reigniteBroadcast);
    }
    private void OnZoneCritical(int zoneIndex) => DecisionLedger.RecordZoneCritical(graph.GetZoneId(zoneIndex), Snapshot());

    /// <summary>烟感点名广播：某区浓烟首次封区，全楼广播该区撤离——楼在听觉上"汇报失守"（限流防连播）。</summary>
    private float nextSmokyBroadcastAt;
    private void OnZoneSmoky(int zoneIndex)
    {
        if (graph == null || graph.Elapsed < nextSmokyBroadcastAt) return;
        nextSmokyBroadcastAt = graph.Elapsed + 14f;
        Broadcast($"消防广播：「{graph.GetZoneId(zoneIndex)}」烟感报警，浓烟已封区——该区域人员请立即压低身体撤离！");
    }

    private void Broadcast(string text)
    {
        if (tutorial != null) tutorial.ShowHint(text);
    }

    /// <summary>当时的火场快照（时间+全楼烟度），作为时间轴条目的归因上下文。</summary>
    private string Snapshot()
    {
        if (graph == null) return null;
        return $"t={graph.Elapsed:F0}s 全楼火势合计见结算";
    }

    /// <summary>死亡验尸：死于哪个区、当时的烟浓度、以及"如果当时……"的反事实建议。</summary>
    private string BuildAutopsy()
    {
        if (graph == null) return null;
        Vector3 pos = playerHealth != null ? playerHealth.transform.position : transform.position;
        string zone = graph.GetZoneIdAt(pos);
        float smoke = graph.GetSmokeAt(pos);

        var sb = new StringBuilder();
        sb.AppendLine($"—— 火场验尸 ——");
        sb.AppendLine($"起火点：{DecisionLedger.GetOriginLabel()}");
        sb.AppendLine($"你倒在「{zone}」（烟浓度 {smoke:F2}，蹲行+湿毛巾能大幅减免烟雾伤害）");
        sb.AppendLine();
        sb.AppendLine("如果重来一次：");
        if (!graph.PowerCutApplied) sb.AppendLine("· 先去配电箱断电——电气火失去电源就翻不了身");
        if (!graph.OriginExtinguished) sb.AppendLine($"· 火刚烧起来时（约前40秒）用灭火器压制「{DecisionLedger.GetOriginLabel()}」还来得及");
        sb.AppendLine("· 穿防火门的路线慢一点，但随手关门能把烟挡在身后");
        sb.AppendLine("· 浓烟中压低身体：烟聚在上层，下层永远是生路");
        return sb.ToString();
    }
}
