using UnityEngine;

/// <summary>玩家生命：火焰近身伤害 + 浓烟分层伤害（蹲下/湿毛巾减免），沉浸式红晕反馈。</summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("参数")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float smokeDPS = 11f;
    [SerializeField, Range(0f, 1f)] private float crouchFactor = 0.35f;
    [SerializeField, Range(0f, 1f)] private float towelFactor = 0.4f;

    [Header("濒死状态（M5：血量低于阈值视野收窄+移动减速）")]
    [SerializeField, Range(0f, 1f)] private float criticalThreshold = 0.30f;
    [SerializeField] private float criticalFov = 50f;
    [SerializeField] private float normalFov = 70f;

    [Header("引用")]
    [SerializeField] private GameHUD hud;
    [SerializeField] private ResultPanelController resultPanel;

    public bool HasWetTowel { get; set; }
    public float Health { get; private set; }
    public bool IsDead { get; private set; }
    /// <summary>濒死状态（M5）：血量低于阈值时触发视野收窄+移动减速。</summary>
    public bool IsCritical { get; private set; }

    private FirstPersonController controller;
    private FirePoint[] firePoints;
    private SmokeVolume[] smokeVolumes;
    private CrawlZone[] crawlZones;
    private float vignetteAlpha;
    private string lastDamageSource = "吸入过量浓烟";

    private void Awake()
    {
        Health = maxHealth;
        controller = GetComponent<FirstPersonController>();
        firePoints = FindObjectsOfType<FirePoint>();
        smokeVolumes = FindObjectsOfType<SmokeVolume>();
        crawlZones = FindObjectsOfType<CrawlZone>();
    }

    private void Update()
    {
        if (IsDead || Time.timeScale <= 0.01f) return;

        Vector3 chest = transform.position + Vector3.up * 0.4f;

        float fireDps = 0f;
        foreach (var fp in firePoints)
            if (fp != null) fireDps += fp.GetDamageAt(chest);

        // B1 蹲行通道：站立重创/蹲下缓慢
        float crawlDps = 0f;
        if (crawlZones != null && controller != null)
            foreach (var cz in crawlZones)
                if (cz != null) crawlDps += cz.GetDamageAt(chest, controller.IsCrouching);

        float smokeDensity = 0f;
        foreach (var sv in smokeVolumes)
            if (sv != null && sv.Contains(chest))
                smokeDensity = Mathf.Max(smokeDensity, sv.Density);
        float smokeDps = 0f;
        if (smokeDensity > 0f)
        {
            float f = smokeDensity;
            if (controller != null && controller.IsCrouching) f *= crouchFactor;
            if (HasWetTowel) f *= towelFactor;
            smokeDps = smokeDPS * f;
        }

        if (fireDps + smokeDps + crawlDps > 0f)
        {
            lastDamageSource = fireDps >= smokeDps + crawlDps ? "被火焰灼伤" : "吸入过量浓烟";
            Health = Mathf.Max(0f, Health - (fireDps + smokeDps + crawlDps) * Time.deltaTime);
        }

        vignetteAlpha = Mathf.Lerp(vignetteAlpha, 1f - Health / maxHealth, 3f * Time.deltaTime);
        if (hud != null) hud.SetVignette(vignetteAlpha);

        // M5 濒死减速：血量低于阈值时视野收窄+移动减速
        bool wasCritical = IsCritical;
        IsCritical = Health < maxHealth * criticalThreshold && !IsDead;
        if (controller != null)
            controller.criticalSpeedFactor = IsCritical ? 0.6f : 1f;
        var cam = Camera.main;
        if (cam != null)
        {
            float targetFov = IsCritical ? criticalFov : normalFov;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 2f * Time.deltaTime);
        }

        if (Health <= 0f) Die();
    }

    private void Die()
    {
        IsDead = true;
        if (resultPanel != null)
        {
            resultPanel.ShowLose(lastDamageSource,
                "逃生要点：浓烟中压低身体前行，用湿毛巾捂住口鼻；\n火焰封锁通道时，用灭火器对准火焰根部开辟生路；\n火势会随时间蔓延——请尽早撤离。");
        }
    }
}
