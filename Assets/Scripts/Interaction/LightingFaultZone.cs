using UnityEngine;

/// <summary>
/// 应急照明故障区（办公楼事件牌）：某区走廊照明整组熄灭+该区烟度下限增益——
/// 考验黑暗低姿摸行；教育点=停电/故障环境里靠出口牌反光与墙缘辨向，压低身体。
/// 导演抽牌后激活（场景预置本组件于 inactive 物体上）。
/// </summary>
public class LightingFaultZone : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private FireZoneGraph graph;
    [SerializeField] private TutorialUI tutorial;
    [Tooltip("本区熄灭的走廊灯（实时 Mixed 灯，运行时关闭无烘焙影响）")]
    [SerializeField] private Light[] lights;

    [Header("参数")]
    [SerializeField] private int zoneIndex = -1;
    [SerializeField, Range(0f, 0.4f)] private float smokeBoost = 0.18f;
    [SerializeField, TextArea(1, 2)] private string activateHint = "这片区的照明故障熄灭了——贴墙、压低身体，靠出口牌的反光辨认方向。";

    public int ZoneIndex => zoneIndex;

    /// <summary>导演调用：熄灯+加烟+广播。</summary>
    public void Activate()
    {
        if (graph == null) graph = FindFirstObjectByType<FireZoneGraph>();
        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialUI>();

        if (lights != null)
            foreach (var l in lights)
                if (l != null) l.enabled = false;

        if (graph != null && zoneIndex >= 0)
            graph.SetZoneSmokeBoost(zoneIndex, smokeBoost);

        DecisionLedger.RecordLightingFault(graph != null && zoneIndex >= 0 ? graph.GetZoneId(zoneIndex) : "故障区");
        if (tutorial != null) tutorial.ShowHint(activateHint);
    }
}
