using UnityEngine;

/// <summary>
/// 公寓房门适配器（M5）：把 demo 的鼠标点击门（opencloseDoor，Animator 驱动 Opening/Closing）
/// 接入项目 E 键交互体系。挂在 DoorFrame 层级上，按 E 直接驱动 Animator 播放开/关动画。
/// 同时接管 demo 门的 open 状态与碰撞体（开启动画后禁用门碰撞，防卡人）。
/// 玩家卧室门额外串联摸门教学：未摸门时按 E 先执行 DoorCheck。
/// </summary>
public class ApartmentDoor : MonoBehaviour, IInteractable
{
    [Tooltip("demo 门脚本（opencloseDoor）。留空自动向下查找。")]
    [SerializeField] private MonoBehaviour demoDoor;
    [Tooltip("门的 Animator（demo 门引用）。留空自动从 demoDoor 查找。")]
    [SerializeField] private Animator doorAnimator;
    [Tooltip("门的碰撞体（开启后禁用防卡人）。留空自动查找 demoDoor 子物体。")]
    [SerializeField] private Collider doorCollider;

    [Header("摸门教学（玩家卧室门配置，其他门留空）")]
    [SerializeField] private DoorCheck doorCheck;

    public string PromptText
    {
        get
        {
            if (IsOpen) return "按 E 关门";
            if (doorCheck != null && !doorCheck.IsChecked) return "按 E 摸门把手判断火情";
            return "按 E 开门";
        }
    }

    public bool CanInteract => ResolveDoor() != null;

    public bool IsOpen { get; private set; }
    /// <summary>摸门教学已完成（LevelFlow 查询用）。等于 doorCheck.IsChecked（未配置时恒 true）。</summary>
    public bool CheckDone => doorCheck == null || doorCheck.IsChecked;

    private void Awake()
    {
        // 自动连同一物体上的 DoorCheck（挂件顺序无关）
        if (doorCheck == null) doorCheck = GetComponent<DoorCheck>();
        ResolveDoor();
    }

    private MonoBehaviour ResolveDoor()
    {
        if (demoDoor != null)
        {
            if (doorAnimator == null) doorAnimator = demoDoor.GetComponent<Animator>();
            if (doorAnimator == null) doorAnimator = demoDoor.GetComponentInChildren<Animator>();
            if (doorCollider == null) doorCollider = demoDoor.GetComponentInChildren<Collider>();
            return demoDoor;
        }
        foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
        {
            var n = mb.GetType().Name;
            if (n == "opencloseDoor" || n == "opencloseDoor1")
            {
                demoDoor = mb;
                if (doorAnimator == null) doorAnimator = mb.GetComponent<Animator>() ?? mb.GetComponentInChildren<Animator>();
                if (doorCollider == null) doorCollider = mb.GetComponentInChildren<Collider>();
                return demoDoor;
            }
        }
        return null;
    }

    public void Interact(PlayerInteraction player)
    {
        // 摸门优先：未摸过门把手时，第一次按 E 执行摸门判断（不开门）
        if (doorCheck != null && !doorCheck.IsChecked)
        {
            doorCheck.Interact(player);
            return;
        }

        if (ResolveDoor() == null)
        {
            Debug.LogWarning("[ApartmentDoor] 找不到 demo 门: " + name);
            return;
        }

        if (doorAnimator == null)
        {
            Debug.LogWarning("[ApartmentDoor] demo 门没有 Animator: " + name);
            return;
        }

        IsOpen = !IsOpen;
        doorAnimator.Play(IsOpen ? "Opening" : "Closing");

        // 同步 demo 门的 open 标志（防止 demo 的 OnMouseOver 与本系统状态不一致）
        var openField = demoDoor.GetType().GetField("open");
        if (openField != null) openField.SetValue(demoDoor, IsOpen);

        // 开门后禁用门碰撞（demo 门动画不处理碰撞，开着仍挡路会卡人）
        if (doorCollider != null) doorCollider.enabled = !IsOpen;
    }
}
