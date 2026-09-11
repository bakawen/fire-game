using UnityEngine;

/// <summary>玩家交互：准星射线检测 + E键交互 + 手持物品挂点。</summary>
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactRange = 3.2f;
    [SerializeField] private Transform holdPoint;

    public IHoldable HeldItem { get; private set; }
    public Transform HoldPoint => holdPoint;

    private IInteractable target;
    private Camera cam;

    private void Awake()
    {
        cam = GetComponentInChildren<Camera>();
        if (holdPoint == null && cam != null)
        {
            var hp = new GameObject("HoldPoint").transform;
            hp.SetParent(cam.transform, false);
            hp.localPosition = new Vector3(0.3f, -0.26f, 0.55f);
            holdPoint = hp;
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0.01f) return;

        FindTarget();
        if (Input.GetKeyDown(KeyCode.E) && target != null && target.CanInteract)
            target.Interact(this);

        HeldItem?.TickHeld();
    }

    private void FindTarget()
    {
        target = null;
        if (cam == null) return;

        // 第一层：精确射线（准星瞄准）
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        var rayHits = Physics.RaycastAll(ray, interactRange, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(rayHits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in rayHits)
        {
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null && interactable.CanInteract)
            {
                target = interactable;
                return;
            }
        }

        // 第二层：球形范围兜底（准星略偏时仍能命中近距离交互物）
        Vector3 probeCenter = cam.transform.position + cam.transform.forward * interactRange * 0.55f;
        var nearby = Physics.OverlapSphere(probeCenter, 1.2f, ~0, QueryTriggerInteraction.Collide);
        float bestDist = float.MaxValue;
        foreach (var col in nearby)
        {
            var interactable = col.GetComponentInParent<IInteractable>();
            if (interactable == null || !interactable.CanInteract) continue;
            float d = Vector3.Distance(cam.transform.position, col.bounds.center);
            if (d < bestDist) { bestDist = d; target = interactable; }
        }
    }

    /// <summary>拾起手持物：挂到相机挂点、禁用碰撞。</summary>
    public void PickUp(IHoldable item, GameObject worldObject)
    {
        HeldItem = item;
        if (holdPoint != null)
        {
            worldObject.transform.SetParent(holdPoint, false);
            worldObject.transform.localPosition = Vector3.zero;
            worldObject.transform.localRotation = Quaternion.identity;
        }
        foreach (var col in worldObject.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    /// <summary>当前准星指向的可交互提示文本（无可交互对象时为 null）。</summary>
    public string TargetPrompt =>
        target != null && target.CanInteract ? target.PromptText : null;
}
