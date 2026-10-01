using System.Collections;
using UnityEngine;

/// <summary>
/// 门后呼救（办公楼中段支线事件）：某扇小门后传出锤门声与呼喊（纯音频事件，无 3D 角色）。
/// 玩家可以花时间破门营救（时间成本+烟暴露），也可以错过——呼救窗口过后声音渐渐消失。
/// 教育点=火场互救原则：先确保自身安全，量力而行。破门后门内的人自己撤离（台词说明），全程无角色模型。
///
/// 挂载方式：本组件挂在门下的"DistressZone"子物体上（带触发碰撞区，比门面略突出让交互射线先命中它）；
/// 同一 GameObject 上不能有两个 IInteractable——PlayerInteraction 的 GetComponentInParent 只认第一个，
/// 所以呼救期间用 InteractableDoor.InteractGate 锁掉门的直接交互（叙事=门被人从里面顶住了），破窗后才恢复。
/// </summary>
public class DistressCall : MonoBehaviour, IInteractable
{
    [Header("音频素材")]
    [SerializeField] private AudioClip poundingClip;   // 锤门循环
    [SerializeField] private AudioClip cryClip;       // 呼救喊声（间隔播放）
    [SerializeField] private AudioClip breakClip;      // 破门
    [SerializeField] private AudioClip rescuedClip;    // 获救台词

    [Header("参数")]
    [SerializeField] private float windowSeconds = 50f;   // 呼救窗口：错过即声停
    [SerializeField] private float cryInterval = 8f;     // 喊声间隔
    [SerializeField] private float interactRange = 2.9f;
    [SerializeField] private float audioRange = 24f;      // 锤门声可闻距离
    [SerializeField] private Transform player;

    [Header("文案")]
    [SerializeField, TextArea(1, 2)] private string rescuedHint = "你把人从门后救了出来——他咳着道谢，自己朝远处出口走了。";

    private InteractableDoor door;
    private AudioSource poundingSrc;
    private AudioSource oneShotSrc;
    private float cryTimer;
    private float lifeTimer;
    private enum State { Active, Rescued, Missed }
    private State state = State.Active;

    public string PromptText => state == State.Active ? "按 E 破门营救（门后有呼救声！）" : null;
    public bool CanInteract => state == State.Active && player != null &&
        Vector3.Distance(player.position, transform.position) <= interactRange;

    private void Awake()
    {
        door = GetComponentInParent<InteractableDoor>();
        if (door != null)
            door.InteractGate = () => state != State.Active;   // 呼救期间门被顶住，不可直接开

        if (player == null)
        {
            var pi = FindFirstObjectByType<PlayerInteraction>();
            if (pi != null) player = pi.transform;
        }
        poundingSrc = gameObject.AddComponent<AudioSource>();
        poundingSrc.clip = poundingClip;
        poundingSrc.loop = true;
        poundingSrc.playOnAwake = false;
        poundingSrc.spatialBlend = 1f;
        poundingSrc.rolloffMode = AudioRolloffMode.Linear;
        poundingSrc.minDistance = 2.5f;
        poundingSrc.maxDistance = audioRange;
        poundingSrc.volume = 0.85f;

        oneShotSrc = gameObject.AddComponent<AudioSource>();
        oneShotSrc.playOnAwake = false;
        oneShotSrc.spatialBlend = 1f;
        oneShotSrc.rolloffMode = AudioRolloffMode.Linear;
        oneShotSrc.minDistance = 2.5f;
        oneShotSrc.maxDistance = audioRange * 1.3f;
    }

    private void OnEnable()
    {
        if (poundingClip != null) poundingSrc.Play();
    }

    private void OnDisable()
    {
        // 模板被导演停用时解锁宿主门：Awake 里上的 InteractGate 引用本组件的 state，
        // 停用后 state 永远停在 Active——不清锁会让宿主门（DoorA(3)）永久不可交互
        if (door != null && door.InteractGate != null)
        {
            door.InteractGate = null;
            door.OpenInterceptor = null;
        }
    }

    private void Start()
    {
        // 记账必须放 Start：Unity 逐物体跑 Awake→OnEnable（不是全部 Awake 后再全部 OnEnable），
        // OnEnable 期写入会被后到的 OfficeDecisionHooks.Awake 的 Reset() 清掉
        DecisionLedger.RecordDistressHeard();
    }

    private void Update()
    {
        if (state != State.Active || Time.timeScale <= 0.01f) return;

        lifeTimer += Time.deltaTime;
        if (lifeTimer >= windowSeconds)
        {
            // 错过：呼救声渐弱消失——不做道德评判，复盘中性记录
            state = State.Missed;
            poundingSrc.Stop();
            return;
        }

        cryTimer += Time.deltaTime;
        if (cryTimer >= cryInterval)
        {
            cryTimer = 0f;
            if (cryClip != null) oneShotSrc.PlayOneShot(cryClip, 0.95f);
        }
    }

    /// <summary>
    /// 导演系统克隆：把本模板（含音频素材与窗口参数）克隆挂到另一扇候选门下——
    /// 事件牌"门后呼救"每局随机换门。克隆件自带触发碰撞区（尺寸取自本模板），
    /// Awake 会自动锁住目标门的直接交互（InteractGate）。
    /// </summary>
    public DistressCall CloneOnto(InteractableDoor targetDoor, float windowOverride = -1f)
    {
        if (targetDoor == null) return null;
        var zone = new GameObject("DistressZone");
        zone.transform.SetParent(targetDoor.transform, false);
        var srcCol = GetComponent<BoxCollider>();
        var col = zone.AddComponent<BoxCollider>();
        col.isTrigger = true;
        if (srcCol != null) { col.size = srcCol.size; col.center = srcCol.center; }
        else { col.size = new Vector3(1.1f, 2.3f, 1.5f); col.center = new Vector3(0f, 1.15f, 0f); }

        var copy = zone.AddComponent<DistressCall>();
        copy.poundingClip = poundingClip;
        copy.cryClip = cryClip;
        copy.breakClip = breakClip;
        copy.rescuedClip = rescuedClip;
        copy.rescuedHint = rescuedHint;
        if (windowOverride > 0f) copy.windowSeconds = windowOverride;
        return copy;
    }

    public void Interact(PlayerInteraction interactor)
    {
        if (state != State.Active) return;
        state = State.Rescued;
        poundingSrc.Stop();
        DecisionLedger.RecordRescue();
        StartCoroutine(BreakOpenRoutine());
    }

    private IEnumerator BreakOpenRoutine()
    {
        if (breakClip != null) oneShotSrc.PlayOneShot(breakClip, 1f);
        yield return new WaitForSeconds(0.8f);
        if (door != null && !door.IsOpen) door.Interact(null);   // 门被撞开（Interact 不查闸）
        yield return new WaitForSeconds(0.9f);
        if (rescuedClip != null) oneShotSrc.PlayOneShot(rescuedClip, 1f);
        var tutorial = FindFirstObjectByType<TutorialUI>();
        if (tutorial != null) tutorial.ShowHint(rescuedHint);
    }
}
