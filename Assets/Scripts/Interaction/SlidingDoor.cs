using UnityEngine;

/// <summary>滑板门（平移门）：按E沿轨道左右平移开合。支持单扇或双扇联动。</summary>
public class SlidingDoor : MonoBehaviour, IInteractable
{
    [Tooltip("左扇（向左滑）。留空则用自身。")]
    [SerializeField] private Transform leftPanel;
    [Tooltip("右扇（向右滑）。可选。")]
    [SerializeField] private Transform rightPanel;
    [Tooltip("开门时平移距离")]
    [SerializeField] private float slideDistance = 1.0f;
    [SerializeField] private float speed = 2.5f;
    [Tooltip("门面板的世界朝向轴（false=沿自身X轴滑动, true=沿自身Z轴滑动）")]
    [SerializeField] private bool slideAlongZ = false;

    public string PromptText => isOpen ? "按 E 关门" : "按 E 开门";
    public bool CanInteract => true;

    private bool isOpen;
    private bool moving;
    private float progress; // 0=关 1=开

    private Vector3 leftClosedPos, leftOpenPos;
    private Vector3 rightClosedPos, rightOpenPos;

    private void Awake()
    {
        if (leftPanel == null) leftPanel = transform;
        leftClosedPos = leftPanel.localPosition;
        rightClosedPos = rightPanel != null ? rightPanel.localPosition : Vector3.zero;

        Vector3 dir = slideAlongZ ? transform.forward : transform.right;
        leftOpenPos = leftClosedPos + dir * slideDistance;
        rightOpenPos = rightClosedPos - dir * slideDistance; // 双扇反向滑动
    }

    public void Interact(PlayerInteraction player)
    {
        isOpen = !isOpen;
        moving = true;
    }

    private void Update()
    {
        if (!moving) return;
        float target = isOpen ? 1f : 0f;
        progress = Mathf.MoveTowards(progress, target, speed * Time.deltaTime / Mathf.Max(slideDistance, 0.01f) * 0.5f);
        progress = Mathf.Clamp01(progress);

        leftPanel.localPosition = Vector3.Lerp(leftClosedPos, leftOpenPos, progress);
        if (rightPanel != null)
            rightPanel.localPosition = Vector3.Lerp(rightClosedPos, rightOpenPos, progress);

        if (Mathf.Approximately(progress, target)) moving = false;
    }
}
