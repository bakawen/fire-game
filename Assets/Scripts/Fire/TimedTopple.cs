using UnityEngine;

/// <summary>
/// 定时倒塌：在指定游戏时间解除 kinematic，让物体因重心偏移自然倒下。
/// 用于公寓 3F 走廊的书架——火势蔓延时失去支撑而倒塌，部分阻挡通道增加紧迫感。
/// </summary>
public class TimedTopple : MonoBehaviour
{
    [Tooltip("倒塌触发时间（秒，自场景加载起算）")]
    public float toppleAtSeconds = 50f;

    [Tooltip("倒塌方向偏移（世界空间，让倒塌有方向性而非原地坍缩）")]
    public Vector3 toppleDirection = new Vector3(0.15f, 0f, 0.1f);

    private Rigidbody rb;
    private bool toppled;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            // 预偏重心：解除 kinematic 后向指定方向倾倒而非原地坍缩
            rb.centerOfMass = transform.InverseTransformPoint(transform.position + toppleDirection.normalized * 0.3f);
        }
    }

    private void Update()
    {
        if (toppled) return;
        if (Time.timeSinceLevelLoad >= toppleAtSeconds)
        {
            toppled = true;
            if (rb != null) rb.isKinematic = false;
        }
    }
}
