using System;
using UnityEngine;

/// <summary>灭火器：按E拾取手持，按住左键喷射白色粉雾；对锥形范围内可灭火点削减强度；剂量有限。</summary>
public class FireExtinguisher : MonoBehaviour, IInteractable, IHoldable
{
    [Header("参数")]
    [SerializeField] private float totalDoseSeconds = 6.5f;
    [SerializeField] private float sprayRange = 3.4f;
    [SerializeField] private float sprayAngle = 32f;
    [SerializeField] private float intensityPerSecond = 0.9f;

    [Header("引用（留空自动创建）")]
    [SerializeField] private ParticleSystem sprayParticles;
    [SerializeField] private Transform nozzleOverride;
    [SerializeField] private Material sprayMaterial;

    [Header("办公楼决策关：对已成势火喷射的教学反转（宿舍关保持关闭）")]
    [SerializeField] private bool trackIneffectiveSpray = false;
    [SerializeField, TextArea(1, 3)] private string ineffectiveHint = "火势已越过初期阶段！灭火器无力回天——立即撤离，不要恋战！";
    [SerializeField] private float ineffectiveHintAtSeconds = 2f;

    /// <summary>被拾取时触发（GuidanceChecklist 推进步骤）。</summary>
    public event Action OnPickedUp;

    public string PromptText => "按 E 拾取灭火器";
    public bool CanInteract => !held;
    public float Dose => dose;

    /// <summary>规则牌"灭火器过期"：剂量减半（巡检签视觉由导演控制）。</summary>
    public void ApplyExpiredInspection() => dose = 0.5f;

    private bool held;
    private float dose = 1f;
    private FirePoint[] firePoints;
    private float bigFireSpray;        // 对不可灭大火的累计喷射时长（教学反转+决策回顾）
    private bool ineffectiveHintShown;

    private void Awake()
    {
        if (sprayParticles == null) BuildSpray();
    }

    public void Interact(PlayerInteraction player)
    {
        held = true;
        player.PickUp(this, gameObject);
        firePoints = FindObjectsOfType<FirePoint>();
        OnPickedUp?.Invoke();
    }

    public void TickHeld()
    {
        bool spraying = Input.GetMouseButton(0) && dose > 0f;
        if (spraying)
        {
            dose = Mathf.Max(0f, dose - Time.deltaTime / totalDoseSeconds);
            if (firePoints == null) firePoints = FindObjectsOfType<FirePoint>();
            ExtinguishInCone();
            // 教学反转（办公楼）：对着已成势的大火喷——剂量在流走但火毫发无损
            if (trackIneffectiveSpray && PointingAtBigFire())
            {
                bigFireSpray += Time.deltaTime;
                DecisionLedger.AddBigFireSpray(Time.deltaTime);
                if (!ineffectiveHintShown && bigFireSpray >= ineffectiveHintAtSeconds)
                {
                    ineffectiveHintShown = true;
                    DecisionLedger.MarkIneffectiveHintShown();
                    var tutorial = FindObjectsOfType<TutorialUI>();
                    if (tutorial != null && tutorial.Length > 0) tutorial[0].ShowHint(ineffectiveHint);
                }
            }
        }
        if (sprayParticles != null)
        {
            if (spraying && !sprayParticles.isPlaying) sprayParticles.Play();
            if (!spraying && sprayParticles.isPlaying) sprayParticles.Stop();
        }
    }

    /// <summary>喷射锥内是否有"燃烧中但不可灭"的火点（对它喷射=无效扑救）。</summary>
    private bool PointingAtBigFire()
    {
        Camera cam = Camera.main;
        if (cam == null || firePoints == null) return false;
        Vector3 origin = cam.transform.position;
        Vector3 dir = cam.transform.forward;
        foreach (var fp in firePoints)
        {
            if (fp == null || !fp.IsBurning || fp.Extinguishable) continue;
            Vector3 to = fp.transform.position + Vector3.up * 0.3f - origin;
            float dist = to.magnitude;
            if (dist > sprayRange + fp.DamageRadius) continue;
            if (Vector3.Angle(dir, to) > sprayAngle) continue;
            return true;
        }
        return false;
    }

    private void ExtinguishInCone()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 origin = cam.transform.position;
        Vector3 dir = cam.transform.forward;

        foreach (var fp in firePoints)
        {
            if (fp == null || !fp.IsBurning || !fp.Extinguishable) continue;
            Vector3 to = fp.transform.position + Vector3.up * 0.3f - origin;
            float dist = to.magnitude;
            if (dist > sprayRange + fp.DamageRadius) continue;
            if (Vector3.Angle(dir, to) > sprayAngle) continue;
            fp.Spray(intensityPerSecond * Time.deltaTime);
        }
    }

    private void BuildSpray()
    {
        Transform parent = nozzleOverride != null ? nozzleOverride : transform;
        var go = new GameObject("Spray");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = nozzleOverride != null ? Vector3.zero : new Vector3(0f, 0.25f, 0.15f);
        // 先停用：AddComponent 时粒子系统会立即自动播放，导致后续设置 duration 被拒绝
        go.SetActive(false);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 9.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.24f);
        main.gravityModifier = 0.18f;
        main.maxParticles = 300;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.92f, 0.94f, 0.96f), Color.white);

        var emission = ps.emission;
        emission.rateOverTime = 85f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 9f;
        shape.radius = 0.03f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.9f, 0.92f, 0.95f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.75f, 0.4f),
                new GradientAlphaKey(0f, 1f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOver = ps.sizeOverLifetime;
        sizeOver.enabled = true;
        sizeOver.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.5f, 0.9f), new Keyframe(1f, 1.7f)));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = sprayMaterial != null
            ? sprayMaterial
            : new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
        renderer.sortingFudge = -5;

        go.SetActive(true);
        sprayParticles = ps;
    }
}
