using UnityEngine;

/// <summary>
/// 断电战术效果（办公楼）：拉闸后——走廊照明熄灭 + 应急指示点亮 + 电气火蔓延减速 + 电梯禁用提示。
/// 订阅配电箱 OnPowerCut 事件（不动 ElectricalPanel 本体），效果全部是运行时可切换的（实时灯关闭/发光贴片激活），
/// 对静态烘焙几何无影响。
/// </summary>
public class PowerCutEffects : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private ElectricalPanel panel;
    [SerializeField] private FireSpreadManager spread;      // 宿舍/公寓旧管理器（办公楼改用 zoneGraph）
    [SerializeField] private FireZoneGraph zoneGraph;       // 办公楼活火场：断电=全局减速+电气起源衰减
    [SerializeField] private ElevatorTrap elevator;

    [Header("走廊照明（拉闸后熄灭的实时灯）")]
    [SerializeField] private Light[] corridorLights;

    [Header("应急指示（拉闸后点亮的自发光贴片，静态几何可见）")]
    [SerializeField] private Renderer[] emergencyGlows;

    [Header("蔓延减速（>1=更晚点燃）")]
    [SerializeField, Range(1f, 2f)] private float spreadDelayFactor = 1.25f;

    private void OnEnable()
    {
        if (panel != null) panel.OnPowerCut += Apply;
    }

    private void OnDisable()
    {
        if (panel != null) panel.OnPowerCut -= Apply;
    }

    private void Apply()
    {
        foreach (var l in corridorLights)
            if (l != null) l.enabled = false;
        foreach (var g in emergencyGlows)
            if (g != null) { g.gameObject.SetActive(true); g.enabled = true; }
        if (spread != null) spread.SlowRemaining(spreadDelayFactor);
        if (zoneGraph != null) zoneGraph.SetPowerCut(true);
        if (elevator != null) elevator.SetPowerOut(true);
        DecisionLedger.RecordPowerCut(zoneGraph != null ? $"火已烧 {zoneGraph.Elapsed:F0} 秒，增长减速" : null);
    }
}