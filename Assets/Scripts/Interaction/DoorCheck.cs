using System;
using UnityEngine;

/// <summary>
/// 摸门判断教学（M5公寓关核心教学点）：交互摸门把手，通过温度判定门外火情。
/// 真实消防知识：门把手烫手说明门外火势已大，应退守室内堵门缝、到窗口求救；
/// 门把手不烫说明门外火势未封锁通道，可低姿开门撤离。
/// 本关主线判定 doorIsHot=false（楼下起火初期热气上涌但门未灼烧）。
/// </summary>
public class DoorCheck : MonoBehaviour, IInteractable
{
    [Header("判定结果文案")]
    [SerializeField, TextArea(1, 3)] private string notHotHint = "门把手只是微温——火势还没有烧到门口。用湿毛巾捂住口鼻，低姿开门，向楼下撤离！";
    [SerializeField, TextArea(1, 3)] private string hotHint = "门把手烫手！门外已被烈焰封锁——立即退回室内，用衣物堵住门缝，到窗口挥动鲜艳衣物求救！";

    [Header("分支配置")]
    [Tooltip("本关判定：true=门烫（固守支线），false=门不烫（撤离主线）")]
    [SerializeField] private bool doorIsHot = false;

    public string PromptText => isChecked ? null : "按 E 摸门把手判断火情";
    public bool CanInteract => !isChecked;

    /// <summary>是否已完成摸门（ApartmentDoor/LevelFlow 查询）。</summary>
    public bool IsChecked => isChecked;

    /// <summary>是否判定为门烫（固守支线）。</summary>
    public bool IsHot => doorIsHot;

    /// <summary>摸门完成（LevelFlow 订阅推进阶段）。</summary>
    public event Action OnDoorChecked;

    private bool isChecked;

    public void Interact(PlayerInteraction player)
    {
        if (isChecked) return;
        isChecked = true;
        OnDoorChecked?.Invoke();
    }

    /// <summary>获取对应判定结果的提示文案（LevelFlow/TutorialUI 调用显示）。</summary>
    public string GetResultHint() => doorIsHot ? hotHint : notHotHint;
}
