using UnityEngine;

/// <summary>烟雾区域：按全局火势提升浓度（伤害+可见浓烟）；玩家在其中受分层伤害（蹲下/湿毛巾减免）。</summary>
public class SmokeVolume : MonoBehaviour
{
    [SerializeField] private Vector3 size = new Vector3(18f, 3f, 3f);
    [SerializeField] private float baseDensity = 0f;
    [SerializeField] private float maxDensity = 1f;

    [Header("可见浓烟（可选：大颗粒慢速烟雾，浓度驱动发射率）")]
    [SerializeField] private ParticleSystem smokePS;
    [SerializeField] private float smokeRateMultiplier = 40f;

    public float Density { get; private set; }

    public bool Contains(Vector3 pos)
    {
        Vector3 c = transform.position;
        Vector3 h = size * 0.5f;
        return Mathf.Abs(pos.x - c.x) <= h.x
            && Mathf.Abs(pos.y - c.y) <= h.y
            && Mathf.Abs(pos.z - c.z) <= h.z;
    }

    public void SetLevel(float globalLevel)
    {
        Density = Mathf.Clamp01(baseDensity + globalLevel * maxDensity);
        if (smokePS != null)
        {
            var em = smokePS.emission;
            em.rateOverTime = Density * smokeRateMultiplier;
        }
    }
}
