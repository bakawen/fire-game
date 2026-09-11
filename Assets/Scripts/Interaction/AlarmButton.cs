using System;
using UnityEngine;

/// <summary>火势系统难度缩放支持：SpawnManager 调用。</summary>
public static class FireSpreadDifficulty
{
}

/// <summary>火警手动报警按钮（A2）：按E触发全楼警铃+灯光闪红，科普“火灾第一件事是报警”。</summary>
public class AlarmButton : MonoBehaviour, IInteractable
{
    [SerializeField] private Light alarmLight;
    [SerializeField] private float flashFrequency = 3f;
    [SerializeField] private AudioSource alarmAudio;
    [SerializeField] private AudioClip alarmClip;
    [Tooltip("报警闪光发光体（静态烘焙场景中实时灯光不可见，用自发光材质保证视觉反馈）。")]
    [SerializeField] private Renderer strobeRenderer;

    public string PromptText => isTriggered ? null : "按 E 触发火警报警";
    public bool CanInteract => !isTriggered;

    /// <summary>报警触发（结算加分/教学提示用）。</summary>
    public event Action OnAlarmTriggered;

    private bool isTriggered;

    public void Interact(PlayerInteraction player)
    {
        isTriggered = true;

        if (alarmAudio != null && alarmClip != null)
        {
            alarmAudio.clip = alarmClip;
            alarmAudio.loop = true;
            alarmAudio.Play();
        }
        else if (alarmAudio != null)
        {
            // 无剪辑时用合成音（440Hz 交替双音）
            alarmAudio.loop = true;
            alarmAudio.Play();
        }

        if (alarmLight != null || strobeRenderer != null) StartCoroutine(FlashRoutine());
        OnAlarmTriggered?.Invoke();
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        float t = 0f;
        var baseColor = new Color(1f, 0.2f, 0.12f);
        while (true)
        {
            t += Time.deltaTime * flashFrequency;
            float pulse = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f;
            if (alarmLight != null) alarmLight.intensity = pulse * 4.5f;
            if (strobeRenderer != null)
                strobeRenderer.material.color = baseColor * (0.25f + pulse * 2.75f);
            yield return null;
        }
    }
}
