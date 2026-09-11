using System;
using UnityEngine;

/// <summary>消防器材箱（A3）：玻璃箱按E破碎取出灭火器，科普“紧急情况可打碎器材箱取用”。</summary>
[RequireComponent(typeof(BoxCollider))]
public class ExtinguisherCabinet : MonoBehaviour, IInteractable
{
    [Header("引用")]
    [SerializeField] private GameObject glassPanel;
    [SerializeField] private FireExtinguisher extinguisherInside;
    [SerializeField] private ParticleSystem shatterParticles;
    [SerializeField] private AudioSource audioSource;

    public string PromptText => opened ? null : "按 E 打碎玻璃取出灭火器";
    public bool CanInteract => !opened;

    /// <summary>取用完成（教学提示用）。</summary>
    public event Action OnTaken;

    private bool opened;

    private void Awake()
    {
        // 确保箱内灭火器初始隐藏
        if (extinguisherInside != null && extinguisherInside.CanInteract)
            SetExtinguisherVisible(false);
    }

    public void Interact(PlayerInteraction player)
    {
        opened = true;

        if (glassPanel != null) glassPanel.SetActive(false);
        if (shatterParticles != null) shatterParticles.Play();
        if (audioSource != null) audioSource.Play();

        // 灭火器交给玩家
        if (extinguisherInside != null)
        {
            SetExtinguisherVisible(true);
            extinguisherInside.Interact(player);
        }
        OnTaken?.Invoke();
    }

    private void SetExtinguisherVisible(bool visible)
    {
        foreach (var r in extinguisherInside.GetComponentsInChildren<Renderer>())
            r.enabled = visible;
    }
}
