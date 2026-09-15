using System.Collections;
using UnityEngine;

/// <summary>
/// 公寓房门适配器（M5）：把 demo 的鼠标点击门（opencloseDoor，Animator 驱动 Opening/Closing）
/// 接入项目 E 键交互体系。直接驱动 demo 脚本内部的 openandclose Animator 播放开/关动画，
/// 门把手等子物体作为 Door 的子层级会自然跟随旋转。
/// 玩家卧室门额外串联摸门教学：未摸门时按 E 先执行 DoorCheck。
/// </summary>
public class ApartmentDoor : MonoBehaviour, IInteractable
{
    [Tooltip("demo 门脚本（opencloseDoor）。留空自动向下查找。")]
    [SerializeField] private MonoBehaviour demoDoor;
    [Tooltip("门板（旋转对象）。留空自动向下查找（Door/Door.001）。")]
    [SerializeField] private Transform doorLeaf;

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
    public bool CheckDone => doorCheck == null || doorCheck.IsChecked;

    private Animator demoAnimator;

    private void Awake()
    {
        if (doorCheck == null) doorCheck = GetComponent<DoorCheck>();
        ResolveDoor();
    }

    private MonoBehaviour ResolveDoor()
    {
        if (demoDoor != null) return demoDoor;
        foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
        {
            var n = mb.GetType().Name;
            if (n == "opencloseDoor" || n == "opencloseDoor1")
            {
                demoDoor = mb;
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

        IsOpen = !IsOpen;
        PlayDoorAnimation(IsOpen);
    }

    /// <summary>通过反射调用 demo 脚本的 opening()/closing() 协程（内部驱动 Animator 播放动画）。</summary>
    private void PlayDoorAnimation(bool opening)
    {
        var t = demoDoor.GetType();
        string methodName = opening ? "opening" : "closing";
        var method = t.GetMethod(methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method != null && method.ReturnType == typeof(IEnumerator))
        {
            var iter = (IEnumerator)method.Invoke(demoDoor, null);
            StartCoroutine(WrapCoroutine(iter));
        }
        else
        {
            // 兜底：直接从 demo 脚本的 openandclose 字段拿 Animator 播放
            var animField = t.GetField("openandclose");
            var anim = animField != null ? animField.GetValue(demoDoor) as Animator : null;
            if (anim != null) anim.Play(opening ? "Opening" : "Closing");
            Debug.LogWarning("[ApartmentDoor] fallback to direct animator play");
        }

        // 同步 open 字段
        var openField = t.GetField("open");
        if (openField != null) openField.SetValue(demoDoor, opening);

        // 碰撞体开关（开门后禁用防卡人）
        var col = GetComponentInChildren<Collider>();
        if (col != null) col.enabled = !opening;
    }

    private IEnumerator WrapCoroutine(IEnumerator inner)
    {
        while (inner.MoveNext()) yield return inner.Current;
    }
}
