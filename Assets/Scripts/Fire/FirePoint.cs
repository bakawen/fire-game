using System;
using UnityEngine;

/// <summary>火点：火焰+烟雾粒子+光照，强度随时间增长；灭火器可扑灭（受可灭标记限制）。</summary>
public class FirePoint : MonoBehaviour
{
    [Header("燃烧参数")]
    [SerializeField] private bool burnOnStart = false;
    [SerializeField] private float startIntensity = 0.3f;
    [SerializeField] private float growthPerSecond = 0.04f;
    [SerializeField] private bool extinguishable = true;

    [Header("伤害")]
    [SerializeField] private float damageRadius = 1.8f;
    [SerializeField] private float contactDPS = 30f;

    [Header("粒子材质（场景配置，留空用内置兜底）")]
    [SerializeField] private Material fireMaterial;
    [SerializeField] private Material smokeMaterial;

    public float Intensity { get; private set; }
    public bool IsBurning => Intensity > 0.03f;
    public bool Extinguishable => extinguishable;
    public float DamageRadius => damageRadius;

    /// <summary>火被扑灭时触发（灭火器扑灭或断电衰减熄灭）。</summary>
    public event Action<FirePoint> OnExtinguished;

    private ParticleSystem firePS;
    private ParticleSystem smokePS;
    private Light fireLight;
    private float externalDecay;
    private float seed;
    private bool extinguishEventFired;

    private void Awake()
    {
        seed = UnityEngine.Random.value * 100f;
        BuildParticles();
        Intensity = burnOnStart ? startIntensity : 0f;
        ApplyVisuals();
    }

    private void Update()
    {
        if (Time.timeScale <= 0.01f || !IsBurning) return;

        if (Intensity < 1f)
            Intensity = Mathf.Min(1f, Intensity + growthPerSecond * Time.deltaTime);

        if (externalDecay > 0f)
        {
            Intensity -= externalDecay * Time.deltaTime;
            if (Intensity <= 0.03f)
            {
                Intensity = 0f;
                FireExtinguished();
            }
        }
        ApplyVisuals();
    }

    public void Ignite()
    {
        if (IsBurning) return;
        extinguishEventFired = false;
        Intensity = Mathf.Max(0.15f, startIntensity);
        ApplyVisuals();
    }

    public void SetGrowth(float growth) => growthPerSecond = growth;

    /// <summary>灭火器喷射削减（不可灭火点忽略）。</summary>
    public void Spray(float amount)
    {
        if (!extinguishable || !IsBurning) return;
        Intensity -= amount;
        if (Intensity <= 0.03f)
        {
            Intensity = 0f;
            FireExtinguished();
        }
        ApplyVisuals();
    }

    /// <summary>外部因素衰减（如断电），不受可灭标记限制。</summary>
    public void StartExternalDecay(float decayPerSecond) => externalDecay = decayPerSecond;

    /// <summary>玩家胸部位置受到的本火点伤害（0 ~ contactDPS*强度，平方衰减）。</summary>
    public float GetDamageAt(Vector3 chestPos)
    {
        if (!IsBurning) return 0f;
        float d = Vector3.Distance(chestPos, transform.position);
        if (d > damageRadius) return 0f;
        float t = 1f - d / damageRadius;
        return contactDPS * Intensity * t * t;
    }

    private void FireExtinguished()
    {
        externalDecay = 0f;
        ApplyVisuals();
        if (extinguishEventFired) return;
        extinguishEventFired = true;
        OnExtinguished?.Invoke(this);
    }

    private void ApplyVisuals()
    {
        if (firePS != null)
        {
            var em = firePS.emission;
            em.rateOverTime = 55f * Intensity;
            if (Intensity > 0.03f) { if (!firePS.isPlaying) firePS.Play(); }
            else if (firePS.isPlaying) firePS.Stop();
        }
        if (smokePS != null)
        {
            var em = smokePS.emission;
            em.rateOverTime = Mathf.Max(10f, 34f * Intensity);
            if (Intensity > 0.03f) { if (!smokePS.isPlaying) smokePS.Play(); }
            else if (smokePS.isPlaying) smokePS.Stop();
        }
        if (fireLight != null)
        {
            fireLight.enabled = Intensity > 0.03f;
            fireLight.intensity = 4.2f * Intensity * (0.82f + 0.18f * Mathf.PerlinNoise(seed, Time.time * 9f));
        }
    }

    private void BuildParticles()
    {
        firePS = CreateParticles("Fire", fireMaterial, true);
        smokePS = CreateParticles("Smoke", smokeMaterial, false);

        var lightGo = new GameObject("FireLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        fireLight = lightGo.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.55f, 0.22f);
        fireLight.range = 7f;
        fireLight.shadows = LightShadows.None;
        fireLight.enabled = false;
    }

    private ParticleSystem CreateParticles(string objName, Material mat, bool isFire)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(transform, false);
        // 先停用：AddComponent 时粒子系统会立即自动播放，导致后续设置 duration 被拒绝
        go.SetActive(false);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = isFire ? 1.2f : 3f;
        main.startLifetime = isFire
            ? new ParticleSystem.MinMaxCurve(0.5f, 0.9f)
            : new ParticleSystem.MinMaxCurve(3.2f, 4.6f);
        main.startSpeed = isFire
            ? new ParticleSystem.MinMaxCurve(1.2f, 2.0f)
            : new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSize = isFire
            ? new ParticleSystem.MinMaxCurve(0.36f, 0.68f)
            : new ParticleSystem.MinMaxCurve(0.9f, 1.7f);
        main.gravityModifier = isFire ? -0.25f : -0.03f;
        main.maxParticles = isFire ? 160 : 180;
        main.startColor = isFire
            ? new ParticleSystem.MinMaxGradient(new Color(1f, 0.62f, 0.15f), new Color(1f, 0.85f, 0.35f))
            : new ParticleSystem.MinMaxGradient(new Color(0.18f, 0.18f, 0.20f), new Color(0.28f, 0.28f, 0.31f));

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = isFire ? 16f : 11f;
        shape.radius = isFire ? 0.16f : 0.3f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        if (isFire)
        {
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.75f, 0.25f), 0f),
                    new GradientColorKey(new Color(0.95f, 0.3f, 0.05f), 0.6f),
                    new GradientColorKey(new Color(0.5f, 0.1f, 0.02f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.7f, 0.5f),
                    new GradientAlphaKey(0f, 1f),
                });
        }
        else
        {
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.18f, 0.18f, 0.2f), 0f),
                    new GradientColorKey(new Color(0.24f, 0.24f, 0.27f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.4f, 0f),
                    new GradientAlphaKey(0.3f, 0.55f),
                    new GradientAlphaKey(0f, 1f),
                });
        }
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOver = ps.sizeOverLifetime;
        sizeOver.enabled = true;
        var curve = isFire
            ? new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.25f))
            : new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.5f, 1.3f), new Keyframe(1f, 2.6f));
        sizeOver.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (mat != null)
        {
            renderer.material = mat;
        }
        else
        {
            renderer.material = new Material(Shader.Find(isFire
                ? "Legacy Shaders/Particles/Additive"
                : "Legacy Shaders/Particles/Alpha Blended"));
        }
        if (isFire) renderer.sortingFudge = -2;

        go.SetActive(true);
        return ps;
    }
}
