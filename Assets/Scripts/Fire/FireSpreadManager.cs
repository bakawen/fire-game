using System.Collections.Generic;
using UnityEngine;

/// <summary>火势蔓延管理：按时间表点燃新火点、增长强度、驱动全局烟雾浓度与远景雾。</summary>
public class FireSpreadManager : MonoBehaviour
{
    [System.Serializable]
    public class SpreadEvent
    {
        public FirePoint firePoint;
        public float igniteAtSeconds = 30f;
        public float growthPerSecond = 0.04f;
    }

    [Header("蔓延时间表")]
    [SerializeField] private List<SpreadEvent> events = new List<SpreadEvent>();

    [Header("烟雾")]
    [SerializeField] private SmokeVolume[] smokeVolumes;

    [Header("远景雾（能见度）")]
    [SerializeField] private float maxFogDensity = 0.055f;

    public float Elapsed { get; private set; }
    public float GlobalSmokeLevel { get; private set; }

    private FirePoint[] allFires;

    /// <summary>难度缩放：随机出生点系统调用（difficulty>1 = 火势更早失控）。</summary>
    public void ApplyDifficultyScale(float difficulty)
    {
        if (difficulty <= 0f) return;
        foreach (var e in events)
            if (e.firePoint != null && !e.firePoint.IsBurning)
                e.igniteAtSeconds /= difficulty;
    }

    /// <summary>查询火点的计划点燃时刻（不在时间表返回 -1）。</summary>
    public float GetIgniteTime(FirePoint fp)
    {
        foreach (var e in events)
            if (e.firePoint == fp) return e.igniteAtSeconds;
        return -1f;
    }

    private void Start()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.44f, 0.42f, 0.39f);
        RenderSettings.fogDensity = 0f;

        if (smokeVolumes == null || smokeVolumes.Length == 0)
            smokeVolumes = FindObjectsOfType<SmokeVolume>();
        allFires = FindObjectsOfType<FirePoint>();
    }

    private void Update()
    {
        if (Time.timeScale <= 0.01f) return;
        Elapsed += Time.deltaTime;

        foreach (var e in events)
        {
            if (e.firePoint == null) continue;
            if (!e.firePoint.IsBurning && Elapsed >= e.igniteAtSeconds)
            {
                e.firePoint.SetGrowth(e.growthPerSecond);
                e.firePoint.Ignite();
            }
        }

        // 全局烟雾 = 场景内所有燃烧火点的总强度（含开局燃烧与蔓延点燃）
        float total = 0f;
        if (allFires != null)
            foreach (var f in allFires)
                if (f != null && f.IsBurning)
                    total += f.Intensity;
        GlobalSmokeLevel = Mathf.Clamp01(total / 4f);

        foreach (var v in smokeVolumes)
            if (v != null) v.SetLevel(GlobalSmokeLevel);

        RenderSettings.fogDensity = Mathf.Lerp(
            RenderSettings.fogDensity,
            GlobalSmokeLevel * maxFogDensity,
            1.2f * Time.deltaTime);
    }
}
