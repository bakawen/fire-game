using System;
using UnityEngine;

/// <summary>湿毛巾：按E拾取后自动捂住口鼻（挂到相机前方），大幅降低烟雾伤害。</summary>
public class WetTowel : MonoBehaviour, IInteractable
{
    public string PromptText => "按 E 拿起湿毛巾";
    public bool CanInteract => true;

    /// <summary>被拾取时触发（教学提示用）。</summary>
    public event Action OnPickedUp;

    public void Interact(PlayerInteraction player)
    {
        var health = player.GetComponent<PlayerHealth>();
        if (health != null) health.HasWetTowel = true;

        var cam = player.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            transform.SetParent(cam.transform, false);
            transform.localPosition = new Vector3(0f, -0.2f, 0.32f);
            transform.localRotation = Quaternion.Euler(72f, 0f, 0f);
        }
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        OnPickedUp?.Invoke();
        Destroy(this);
    }
}
