using UnityEngine;

/// <summary>可交互物接口：玩家准星对准并按 E 时触发。</summary>
public interface IInteractable
{
    string PromptText { get; }
    bool CanInteract { get; }
    void Interact(PlayerInteraction player);
}

/// <summary>可手持物接口：拾取后每帧调用（处理左键使用等）。</summary>
public interface IHoldable
{
    void TickHeld();
}
