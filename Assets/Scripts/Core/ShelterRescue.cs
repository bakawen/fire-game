using System;
using UnityEngine;

/// <summary>
/// B线固守待援：堵门缝交互 + 窗口求救。
/// 挂在2F安全户的关门上，玩家交互堵门缝→窗口交互求救→触发消防车到达→固守结局。
/// </summary>
public class ShelterRescue : MonoBehaviour, IInteractable
{
    public enum RescueStep { SealDoor, WaveWindow, Waiting, Rescued }

    [Header("引用")]
    [SerializeField] private Transform doorSealPoint;
    [SerializeField] private Transform windowPoint;
    [SerializeField] private AudioSource sirenSource;

    [Header("进度提示")]
    [SerializeField, TextArea(1, 2)] private string sealDoneHint = "门缝已堵严实——浓烟进不来了。去窗口呼救！";
    [SerializeField, TextArea(1, 2)] private string waveDoneHint = "消防员看到你了！他们正在架设云梯——坚持住！";

    public string PromptText => currentStep == RescueStep.SealDoor ? "按 E 用湿衣物堵住门缝"
        : currentStep == RescueStep.WaveWindow ? "按 E 挥动鲜艳衣物求救"
        : null;
    public bool CanInteract => currentStep == RescueStep.SealDoor || currentStep == RescueStep.WaveWindow;

    public event Action<ShelterRescue> OnRescueComplete;

    private RescueStep currentStep = RescueStep.SealDoor;
    private TutorialUI tutorial;
    private float holdTimer;
    private const float holdDuration = 2.5f;
    private bool interacting;

    private void Start()
    {
        tutorial = FindFirstObjectByType<TutorialUI>();
    }

    private void Update()
    {
        if (!interacting) return;
        holdTimer += Time.deltaTime;
        if (holdTimer >= holdDuration) Complete();
    }

    public void Interact(PlayerInteraction player)
    {
        if (interacting) return;
        interacting = true;
        holdTimer = 0f;
    }

    public void CancelInteract()
    {
        interacting = false;
        holdTimer = 0f;
    }

    private void Complete()
    {
        interacting = false;
        switch (currentStep)
        {
            case RescueStep.SealDoor:
                currentStep = RescueStep.WaveWindow;
                if (tutorial != null) tutorial.ShowHint(sealDoneHint);
                break;
            case RescueStep.WaveWindow:
                currentStep = RescueStep.Waiting;
                if (tutorial != null) tutorial.ShowHint(waveDoneHint);
                if (sirenSource != null) sirenSource.Play();
                StartCoroutine(RescueArrive(5f));
                break;
        }
    }

    private System.Collections.IEnumerator RescueArrive(float delay)
    {
        yield return new WaitForSeconds(delay);
        currentStep = RescueStep.Rescued;
        OnRescueComplete?.Invoke(this);
    }
}
