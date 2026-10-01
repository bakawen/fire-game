using System;
using UnityEngine;

/// <summary>可开关的门：按E绕铰链旋转开合；支持双扇门（B叶反向旋转）。</summary>
public class InteractableDoor : MonoBehaviour, IInteractable
{
    [Tooltip("铰链节点（门轴）。留空则用自身。")]
    [SerializeField] private Transform hinge;
    [Tooltip("第二扇门叶（双开门反向旋转）。可留空。")]
    [SerializeField] private Transform hingeB;
    [SerializeField] private float openAngle = 105f;
    [SerializeField] private float speed = 130f;

    public string PromptText => isOpen ? "按 E 关门" : "按 E 开门";

    /// <summary>开启拦截器（QuizDoor 等设置：拦截时按E不直接开门，转由拦截器处理）。</summary>
    public Func<PlayerInteraction, bool> OpenInterceptor { get; set; }

    /// <summary>外部交互闸（呼救事件等接管期间返回 false：本门不可直接交互）。空=正常可交互。</summary>
    public Func<bool> InteractGate { get; set; }

    public bool CanInteract => InteractGate == null || InteractGate();

    /// <summary>门当前是否开启（QuizDoor 等外部系统查询）。</summary>
    public bool IsOpen => isOpen;

    /// <summary>外部强制开合状态（炸门等演出用）：置位+广播事件，不播动画。</summary>
    public void ForceSetOpen(bool open)
    {
        if (isOpen == open) return;
        isOpen = open;
        moving = false;
        if (open) OnOpened?.Invoke(this);
        else OnClosed?.Invoke(this);
    }

    /// <summary>门被打开时触发（教学提示用）。</summary>
    public event Action<InteractableDoor> OnOpened;

    /// <summary>门被关闭时触发（办公楼"防火门随手关挡烟"决策用）。</summary>
    public event Action<InteractableDoor> OnClosed;

    private bool isOpen;
    private bool moving;
    // 用累计角度而非读回localEulerAngles，避免360°回绕问题
    private float closedYA, angleA;
    private float closedYB, angleB;
    private bool hasB;

    private void Awake()
    {
        if (hinge == null) hinge = transform;
        hasB = hingeB != null;
        closedYA = hinge.localEulerAngles.y;
        angleA = 0f;
        if (hasB)
        {
            closedYB = hingeB.localEulerAngles.y;
            angleB = 0f;
        }
    }

    public void Interact(PlayerInteraction player)
    {
        // 拦截器（问答门等）：返回 true 表示已处理，本次不开门
        if (!isOpen && OpenInterceptor != null && OpenInterceptor(player)) return;

        isOpen = !isOpen;
        moving = true;
        if (isOpen) OnOpened?.Invoke(this);
        else OnClosed?.Invoke(this);   // 由开到关（玩家随手关门/再关门）
    }

    private void Update()
    {
        if (!moving) return;
        float targetA = isOpen ? openAngle : 0f;
        angleA = Mathf.MoveTowards(angleA, targetA, speed * Time.deltaTime);
        hinge.localRotation = Quaternion.Euler(0f, closedYA + angleA, 0f);

        if (hasB)
        {
            // B叶反向摆动
            float targetB = isOpen ? -openAngle : 0f;
            angleB = Mathf.MoveTowards(angleB, targetB, speed * Time.deltaTime);
            hingeB.localRotation = Quaternion.Euler(0f, closedYB + angleB, 0f);
        }

        bool aDone = Mathf.Approximately(angleA, targetA);
        bool bDone = !hasB || Mathf.Approximately(angleB, isOpen ? -openAngle : 0f);
        if (aDone && bDone) moving = false;
    }
}
