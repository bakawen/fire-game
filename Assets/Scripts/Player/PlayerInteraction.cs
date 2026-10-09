using UnityEngine;

/// <summary>玩家交互：准星射线检测 + E键交互（长按型带按住判定与取消）+ 手持物品挂点。</summary>
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactRange = 3.2f;
    [SerializeField] private Transform holdPoint;

    public IHoldable HeldItem { get; private set; }
    public Transform HoldPoint => holdPoint;

    private IInteractable target;
    private IInteractable holdTarget;   // 本次按下已 Interact 的长按目标（松手/走远即取消）
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

        // 长按判定：走离目标范围即取消（对准星偏移宽容——只看物理距离）
        if (holdTarget != null)
        {
            var mb = holdTarget as MonoBehaviour;
            if (mb == null || Vector3.Distance(transform.position, mb.transform.position) > interactRange * 1.6f)
            {
                CancelHold();
                holdTarget = null;
            }
        }

        if (Input.GetKeyDown(KeyCode.E) && target != null && target.CanInteract)
        {
            target.Interact(this);
            holdTarget = target is IHoldInteractable ? target : null;   // 仅长按型进入按住跟踪
            var sway = GetComponent<HandSway>();
            if (sway != null) sway.TriggerReach();
        }

        // 长按判定：松手提前取消（与"按住 E"提示语对齐）
        if (Input.GetKeyUp(KeyCode.E) && holdTarget != null)
        {
            CancelHold();
            holdTarget = null;
        }

        HeldItem?.TickHeld();
    }

    /// <summary>取消未完成的长按（已完成的步骤不受影响）。</summary>
    private void CancelHold()
    {
        if (holdTarget is IHoldInteractable h)
        {
            float p = h.HoldProgress;
            if (p >= 0f && p < 1f) h.CancelInteract();
        }
    }

    /// <summary>当前长按进度（0~1=按住中；-1=无长按交互，HUD 据此显隐进度圈）。</summary>
    public float HoldProgress
    {
        get
        {
            if (holdTarget is IHoldInteractable h) return h.HoldProgress;
            return -1f;
        }
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

    /// <summary>拾起手持物：挂到相机挂点、禁用碰撞；灭火器切换第一人称手臂视图模型。</summary>
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

        // 手持灭火器时切换 FP_Arms_VM（手臂+灭火器视图模型）；
        // 世界灭火器只关 MeshRenderer——喷射粒子（ParticleSystemRenderer）保留照常喷射
        if (item is FireExtinguisher && cam != null)
        {
            var vm = cam.transform.Find("FP_Arms_VM");
            if (vm != null)
            {
                vm.gameObject.SetActive(true);
                foreach (var r in worldObject.GetComponentsInChildren<MeshRenderer>())
                    r.enabled = false;
            }
        }
    }

    /// <summary>当前准星指向的可交互提示文本（无可交互对象时为 null）。</summary>
    public string TargetPrompt =>
        target != null && target.CanInteract ? target.PromptText : null;
}
