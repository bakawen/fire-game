using UnityEngine;

/// <summary>
/// 路线选择触发器（办公楼活火场）：玩家进入本区即记录一次路线选择（近路/安全路，以第一次为准），
/// 同时把地面引导箭头切换到本条路线的途经链——路线由玩家自己读火场决定，箭头只跟随选择。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class RouteTrigger : MonoBehaviour
{
    [Tooltip("true=穿火近路（A），false=安全疏散路线（B）")]
    [SerializeField] private bool isShortcutRoute;
    [Tooltip("本条路线的地面箭头途经链（空=最短路）")]
    [SerializeField] private Transform[] routeChain;
    [SerializeField] private FireZoneGraph graph;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<FirstPersonController>() == null) return;

        string ctx = null;
        if (graph != null) ctx = $"该路线烟浓度 {graph.GetSmokeAt(transform.position):F2}";

        // 首次进入=路线选择记录；已选过路线又进入另一条=中途折返（真实火场里回头是活命判断）
        bool hadRoute = DecisionLedger.HasRoute();
        DecisionLedger.RecordRoute(isShortcutRoute, ctx);
        if (hadRoute && DecisionLedger.RouteIsShortcut != isShortcutRoute)
            DecisionLedger.RecordTurnback(isShortcutRoute, ctx);

        if (routeChain != null && routeChain.Length > 0)
        {
            var guide = FindFirstObjectByType<GuidanceChecklist>();
            if (guide != null) guide.SetRouteChain(routeChain);
        }
    }
}
