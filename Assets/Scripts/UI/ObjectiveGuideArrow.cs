using UnityEngine;

/// <summary>
/// 当前目标物的 3D 浮动引导箭头（运行时自建 SpriteRenderer）：
/// 始终朝向相机 + 上下浮动 + 轻微缩放脉动；玩家贴近时淡出避免挡视线。
/// 由 GuidanceChecklist 驱动，随步骤推进移动到下一个目标物。
/// </summary>
public class ObjectiveGuideArrow : MonoBehaviour
{
    [SerializeField] private float bobAmplitude = 0.12f;
    [SerializeField] private float bobSpeed = 2.4f;
    [SerializeField] private float pulseAmplitude = 0.08f;
    [SerializeField] private float pulseSpeed = 3.2f;
    [SerializeField] private float fadeDistance = 2.4f;

    private Transform followTarget;
    private float heightOffset = 1.6f;
    private SpriteRenderer spriteRenderer;
    private Color baseColor = new Color(1f, 0.62f, 0.18f, 1f);
    private float timer;
    private float baseScale = 1f;

    public static ObjectiveGuideArrow Create(Sprite arrowSprite)
    {
        var go = new GameObject("GuideArrow");
        var arrow = go.AddComponent<ObjectiveGuideArrow>();
        arrow.spriteRenderer = go.AddComponent<SpriteRenderer>();
        arrow.spriteRenderer.sprite = arrowSprite;
        arrow.spriteRenderer.sortingOrder = 20;
        arrow.baseColor = new Color(1f, 0.62f, 0.18f, 1f);
        arrow.spriteRenderer.color = arrow.baseColor;
        // Sprite 原生 PPU 尺寸可达数米，统一缩放到约 0.6m 宽
        if (arrowSprite != null && arrowSprite.bounds.size.x > 0.001f)
        {
            arrow.baseScale = 0.6f / arrowSprite.bounds.size.x;
            go.transform.localScale = Vector3.one * arrow.baseScale;
        }
        return arrow;
    }

    public void SetTarget(Transform target, float heightOffset)
    {
        followTarget = target;
        this.heightOffset = heightOffset;
        if (spriteRenderer != null) spriteRenderer.enabled = target != null;
        transform.position = TargetPoint();
    }

    public void Hide()
    {
        followTarget = null;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    private Vector3 TargetPoint()
    {
        if (followTarget == null) return transform.position;
        return followTarget.position + Vector3.up * heightOffset;
    }

    private void LateUpdate()
    {
        if (followTarget == null || spriteRenderer == null || !spriteRenderer.enabled) return;

        timer += Time.unscaledDeltaTime;
        var cam = Camera.main;
        if (cam != null)
        {
            // 公告板朝向相机
            transform.rotation = cam.transform.rotation;

            // 贴近淡出（走到目标跟前时箭头别挡视线）
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            float alpha = Mathf.Clamp01((dist - 1.1f) / (fadeDistance - 1.1f));
            var c = baseColor; c.a *= alpha;
            spriteRenderer.color = c;
        }

        float bob = Mathf.Sin(timer * bobSpeed) * bobAmplitude;
        transform.position = TargetPoint() + Vector3.up * bob;
        float pulse = 1f + Mathf.Sin(timer * pulseSpeed) * pulseAmplitude;
        transform.localScale = Vector3.one * (baseScale * pulse);
    }
}
