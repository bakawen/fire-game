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

/// <summary>长按型交互：按下 E 开始计时，提前松手/走远即取消（准星进度圈反馈，GameHUD 消费）。</summary>
public interface IHoldInteractable : IInteractable
{
    float HoldSeconds { get; }
    /// <summary>0~1=按住中；-1=未在按住（未开始/已完成），HUD 据此显隐进度圈。</summary>
    float HoldProgress { get; }
    /// <summary>取消未完成的按住（进度回零；已完成的步骤不受影响）。</summary>
    void CancelInteract();
}
