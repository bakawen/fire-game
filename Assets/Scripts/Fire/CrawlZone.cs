using System;
using UnityEngine;

/// <summary>强制蹲行通道（B1）：浓烟极浓的走廊段，站立通过受重创、蹲下缓慢通过——低姿前行的强制实践。</summary>
public class CrawlZone : MonoBehaviour
{
    [Header("几何（默认按BoxCollider）")]
    [SerializeField] private float highSmokeStandingDPS = 34f;
    [SerializeField] private float highSmokeCrouchDPS = 6f;

    /// <summary>玩家站立进入浓烟区（教学提示用，一次性触发）。</summary>
    public event Action OnStandingDamage;

    private BoxCollider zone;
    private bool hintFired;

    private void Awake()
    {
        zone = GetComponent<BoxCollider>();
        if (zone == null) zone = gameObject.AddComponent<BoxCollider>();
        zone.isTrigger = true;
    }

    /// <summary>本区域内玩家应受的烟雾伤害（站立/蹲下不同）。</summary>
    public float GetDamageAt(Vector3 chestPos, bool crouching)
    {
        var b = zone.bounds;
        if (chestPos.x < b.min.x || chestPos.x > b.max.x
            || chestPos.y < b.min.y || chestPos.y > b.max.y
            || chestPos.z < b.min.z || chestPos.z > b.max.z) return 0f;

        if (!crouching && !hintFired)
        {
            hintFired = true;
            OnStandingDamage?.Invoke();
        }
        return crouching ? highSmokeCrouchDPS : highSmokeStandingDPS;
    }
}
