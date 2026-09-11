using System;
using UnityEngine;

/// <summary>配电箱：按E拉闸断电，使关联的电气火源持续衰减熄灭（电气火先断电的知识点）。</summary>
public class ElectricalPanel : MonoBehaviour, IInteractable
{
    [SerializeField] private FirePoint[] affectedFires;
    [Tooltip("手柄节点（拉闸后旋转视觉）。")]
    [SerializeField] private Transform lever;
    [SerializeField] private float decayPerSecond = 0.05f;

    public string PromptText => isOff ? null : "按 E 拉闸断电";
    public bool CanInteract => !isOff;

    /// <summary>断电时触发。</summary>
    public event Action OnPowerCut;

    private bool isOff;

    public void Interact(PlayerInteraction player)
    {
        isOff = true;
        foreach (var f in affectedFires)
            if (f != null) f.StartExternalDecay(decayPerSecond);
        if (lever != null) lever.localRotation = Quaternion.Euler(-42f, 0f, 0f);
        OnPowerCut?.Invoke();
    }
}
