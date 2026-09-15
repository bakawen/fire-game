using System;
using UnityEngine;

/// <summary>火点：火焰+烟雾+火星粒子+光照，强度随时间增长；灭火器可扑灭（受可灭标记限制）。
/// 视觉基调：大火+竖向火舌(3D尺寸拉伸)+噪声乱舞+火星飞溅+火光摇曳照亮环境
/// （配合火区周边几何取消静态标记，见WC区先例）。</summary>
public class FirePoint : MonoBehaviour
{
    private enum ParticleKind { Fire, Smoke, Ember }

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
    private ParticleSystem emberPS;
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
            em.rateOverTime = 95f * Intensity;
            if (Intensity > 0.03f) { if (!firePS.isPlaying) firePS.Play(); }
            else if (firePS.isPlaying) firePS.Stop();
        }
        if (smokePS != null)
        {
            var em = smokePS.emission;
            em.rateOverTime = Mathf.Max(14f, 48f * Intensity);
            if (Intensity > 0.03f) { if (!smokePS.isPlaying) smokePS.Play(); }
            else if (smokePS.isPlaying) smokePS.Stop();
        }
        if (emberPS != null)
        {
            var em = emberPS.emission;
            em.rateOverTime = 42f * Intensity;
            if (Intensity > 0.03f) { if (!emberPS.isPlaying) emberPS.Play(); }
            else if (emberPS.isPlaying) emberPS.Stop();
        }
        if (fireLight != null)
        {
            fireLight.enabled = Intensity > 0.03f;
            // 双频闪烁：低频摇曳打底 + 高频抖动叠加，火光永不熄灭式归零
            float flick = Mathf.PerlinNoise(seed, Time.time * 6.5f) * 0.6f
                        + Mathf.PerlinNoise(seed + 37f, Time.time * 16f) * 0.4f;
            fireLight.intensity = 6.5f * Intensity * (0.72f + 0.35f * flick);
        }
    }

    private void BuildParticles()
    {
        firePS = CreateParticles("Fire", fireMaterial, ParticleKind.Fire);
        smokePS = CreateParticles("Smoke", smokeMaterial, ParticleKind.Smoke);
        emberPS = CreateParticles("Embers", fireMaterial, ParticleKind.Ember);

        var lightGo = new GameObject("FireLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        fireLight = lightGo.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.42f, 0.12f);
        fireLight.range = 11f;
        fireLight.shadows = LightShadows.None;
        fireLight.enabled = false;
    }

    private ParticleSystem CreateParticles(string objName, Material mat, ParticleKind kind)
    {
        bool isFire = kind == ParticleKind.Fire;
        var go = new GameObject(objName);
        go.transform.SetParent(transform, false);
        // 先停用：AddComponent 时粒子系统会立即自动播放，导致后续设置 duration 被拒绝
        go.SetActive(false);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = isFire ? 1.4f : 3f;
        switch (kind)
        {
            case ParticleKind.Fire:
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.62f, 1.05f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.7f, 3.1f);
                // 竖向拉伸的火舌形态（X宽Y高），噪声扰动下呈跳动的火舌而非光球
                main.startSize3D = true;
                main.startSizeX = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
                main.startSizeY = new ParticleSystem.MinMaxCurve(1.5f, 2.8f);
                main.startSizeZ = new ParticleSystem.MinMaxCurve(1f, 1f);
                main.gravityModifier = -0.42f;
                main.maxParticles = 300;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.55f, 0.10f), new Color(1f, 0.90f, 0.45f));
                break;
            case ParticleKind.Smoke:
                main.startLifetime = new ParticleSystem.MinMaxCurve(3.2f, 4.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
                main.gravityModifier = -0.03f;
                main.maxParticles = 240;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.18f, 0.18f, 0.20f), new Color(0.28f, 0.28f, 0.31f));
                break;
            case ParticleKind.Ember:
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.0f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
                main.gravityModifier = -0.12f;
                main.maxParticles = 140;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.80f, 0.35f), new Color(1f, 0.95f, 0.60f));
                break;
        }

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = kind == ParticleKind.Fire ? 30f : (kind == ParticleKind.Ember ? 24f : 14f);
        shape.radius = kind == ParticleKind.Fire ? 0.4f : (kind == ParticleKind.Ember ? 0.25f : 0.45f);
        shape.rotation = new Vector3(-90f, 0f, 0f);

        // 噪声=火舌乱舞：真火感的核心。火与火星强扰动，烟弱扰动
        var noise = ps.noise;
        noise.enabled = true;
        if (kind == ParticleKind.Fire)
        {
            noise.strength = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            noise.frequency = 0.7f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
        }
        else if (kind == ParticleKind.Ember)
        {
            noise.strength = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
            noise.frequency = 1.1f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
        }
        else
        {
            noise.strength = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            noise.frequency = 0.3f;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }
        noise.scrollSpeed = 0.35f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        if (isFire)
        {
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.80f, 0.30f), 0f),
                    new GradientColorKey(new Color(0.98f, 0.35f, 0.05f), 0.6f),
                    new GradientColorKey(new Color(0.5f, 0.10f, 0.02f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.7f, 0.5f),
                    new GradientAlphaKey(0f, 1f),
                });
        }
        else if (kind == ParticleKind.Ember)
        {
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.80f, 0.35f), 0f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.10f), 0.7f),
                    new GradientColorKey(new Color(0.6f, 0.12f, 0.02f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.6f),
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
                    new GradientAlphaKey(0.42f, 0f),
                    new GradientAlphaKey(0.32f, 0.55f),
                    new GradientAlphaKey(0f, 1f),
                });
        }
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOver = ps.sizeOverLifetime;
        sizeOver.enabled = true;
        AnimationCurve curve;
        if (isFire)
            curve = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.3f, 1.1f), new Keyframe(1f, 0.1f));
        else if (kind == ParticleKind.Ember)
            curve = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f));
        else
            curve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.5f, 1.3f), new Keyframe(1f, 2.6f));
        sizeOver.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (mat != null)
        {
            renderer.material = mat;
        }
        else
        {
            renderer.material = new Material(Shader.Find(isFire || kind == ParticleKind.Ember
                ? "Legacy Shaders/Particles/Additive"
                : "Legacy Shaders/Particles/Alpha Blended"));
        }
        if (kind != ParticleKind.Smoke) renderer.sortingFudge = -2;
        if (kind == ParticleKind.Ember) renderer.sortingFudge = -3;

        go.SetActive(true);
        return ps;
    }
}
