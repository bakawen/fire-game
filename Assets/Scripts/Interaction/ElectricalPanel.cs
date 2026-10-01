using System;
using UnityEngine;

/// <summary>配电箱：按E拉闸断电，使关联的电气火源持续衰减熄灭（电气火先断电的知识点）。</summary>
public class ElectricalPanel : MonoBehaviour, IInteractable
{
    [SerializeField] private FirePoint[] affectedFires;
    [Tooltip("手柄节点（拉闸后旋转视觉）。")]
    [SerializeField] private Transform lever;
    [SerializeField] private float decayPerSecond = 0.05f;

    public string PromptText => (isOff || Gated) ? null : "按 E 拉闸断电";
    public bool CanInteract => !isOff && !Gated;

    /// <summary>外部交互闸（规则牌"停电检修"等：返回 false=本局不可拉闸，提示不显示）。</summary>
    public Func<bool> InteractGate { get; set; }

    /// <summary>当前是否被外部封禁（导演"配电检修"牌）。</summary>
    public bool Gated => InteractGate != null && !InteractGate();

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
