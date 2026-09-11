using UnityEngine;

/// <summary>火焰蔓延预警闪光（C3）：火点点燃前数秒该处小烟+红光闪烁，提示玩家提前绕行。</summary>
public class SpreadWarning : MonoBehaviour
{
    [Header("参数")]
    [SerializeField] private FirePoint targetFire;
    [SerializeField] private float warnSecondsBefore = 4f;
    [SerializeField] private float pulseSpeed = 4f;
    [SerializeField] private float maxLightIntensity = 2.6f;
    [Tooltip("预警发光体（静态烘焙场景中实时灯光不可见，用自发光材质保证视觉反馈）。")]
    [SerializeField] private Renderer strobeRenderer;

    private FireSpreadManager mgr;
    private Light warnLight;
    private bool warning;

    private void Start()
    {
        mgr = FindObjectOfType<FireSpreadManager>();
        warnLight = GetComponent<Light>();
        if (warnLight != null) { warnLight.enabled = false; warnLight.color = new Color(1f, 0.2f, 0.1f); }

        if (targetFire != null)
            targetFire.OnExtinguished += _ => StopWarning();
    }

    private void Update()
    {
        if (mgr == null || targetFire == null) return;
        if (targetFire.IsBurning) { StopWarning(); return; }

        // 计算该火点预计点燃时间与剩余时间
        float igniteAt = mgr.GetIgniteTime(targetFire);
        if (igniteAt < 0f) { StopWarning(); return; }

        float remain = igniteAt - mgr.Elapsed;
        if (remain <= warnSecondsBefore && remain > 0f)
        {
            if (!warning) warning = true;
            float pulse = Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) * 0.5f + 0.5f;
            if (warnLight != null)
            {
                warnLight.enabled = true;
                warnLight.intensity = pulse * maxLightIntensity;
            }
            if (strobeRenderer != null)
                strobeRenderer.material.color = new Color(1f, 0.2f, 0.1f) * (0.2f + pulse * 2.6f);
        }
        else StopWarning();
    }

    private void StopWarning()
    {
        warning = false;
        if (warnLight != null) warnLight.enabled = false;
        if (strobeRenderer != null) strobeRenderer.material.color = new Color(0f, 0f, 0f, 0f);
    }
}
