using System;
using UnityEngine;

/// <summary>
/// 电梯陷阱（B3·停电版）：按E触发故障——错误蜂鸣+教学提示"火场严禁乘电梯"。
/// 电梯门保持关闭（停电打不开=更真实），沿绿色指示从主入口撤离。
/// 仅04_Office使用。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ElevatorTrap : MonoBehaviour, IInteractable
{
    [Header("音效")]
    [SerializeField] private AudioClip errorClip;       // QA包自带ElevatorError.wav
    [SerializeField] private AudioClip faultClip;       // ElevatorFault.wav三连降音
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    [Header("冷却")]
    [SerializeField] private float cooldownSeconds = 3f;

    public string PromptText => CanInteract ? "按 E 乘坐电梯" : null;
    public bool CanInteract => Time.time - lastInteract > cooldownSeconds;

    /// <summary>玩家尝试乘电梯（教学提示用）。</summary>
    public event Action OnTriedElevator;

    private AudioSource audioSrc;
    private float lastInteract = -999f;

    private void Awake()
    {
        audioSrc = GetComponent<AudioSource>();
        if (audioSrc == null) audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 1f;
        audioSrc.minDistance = 1f;
        audioSrc.maxDistance = 8f;
    }

    public void Interact(PlayerInteraction player)
    {
        if (!CanInteract) return;
        lastInteract = Time.time;

        if (audioSrc != null)
        {
            if (errorClip != null) audioSrc.PlayOneShot(errorClip, volume);
            if (faultClip != null) audioSrc.PlayOneShot(faultClip, volume);
        }

        OnTriedElevator?.Invoke();
    }
}
