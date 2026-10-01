/// <summary>同局重试：结算面板"重新开始"置 Requested；关卡加载时 TutorialUI.Awake 捕获为 Active。
/// 消费方：FireZoneGraph.PickOrigin（同一随机起火点）与 SpawnManager（同出生点）——
/// 死亡后带着教训重做同一道"火场题"，教育游戏的错题重做闭环。
/// 宿舍/公寓无消费方（无图/出生系统禁用），置位也无副作用。</summary>
public static class RetryRun
{
    public static bool Requested;          // 结算重试按钮置 true
    public static bool Active;             // 本局是否为同局重试（关卡加载时捕获）
    public static int OriginIndex = -1;   // 办公楼活火场：上一局起火点下标
    public static int SpawnIndex = -1;    // 随机出生：上一局出生点下标
}
