using UnityEngine;

/// <summary>
/// 被堵死的"安全出口"（办公楼中段事件）：挂着 EXIT 绿牌、看起来是出路，推开门却发现被杂物死死堵住——
/// 把"折返"从理论变成必然经历一次的教学；教育点=安全出口必须保持通畅（消防检查核心项），
/// 以及"永远记住两条以上疏散路线"。用 OpenInterceptor 模式拦截开门（门永远推不开）。
/// </summary>
[RequireComponent(typeof(InteractableDoor))]
public class BlockedExitDoor : MonoBehaviour
{
    [SerializeField, TextArea(2, 3)] private string blockedHint =
        "推不开！这扇「安全出口」被杂物死死堵住了！\n真实火场里，被占用的安全出口是致命陷阱——请永远记住两条以上疏散路线。";
    [SerializeField] private TutorialUI tutorial;

    private InteractableDoor door;
    private bool hinted;

    private void Awake()
    {
        door = GetComponent<InteractableDoor>();
        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialUI>();
        // 永远拦截开门：推不开 + 教育提示（首推播报，之后短提示）
        door.OpenInterceptor = p =>
        {
            DecisionLedger.RecordBlockedExitTry();
            if (tutorial != null) tutorial.ShowHint(hinted ? "门后堵死了，快换路线！" : blockedHint);
            hinted = true;
            return true;
        };
    }
}
