using UnityEngine;

/// <summary>可交互物标记：在关键道具上方浮动图标+文字（公告板），任务完成后自动隐藏、超出距离隐藏。</summary>
public class InteractableMarker : MonoBehaviour
{
    [Header("完成判定源（按需配置，任一条件满足即隐藏）")]
    [SerializeField] private AlarmButton alarmButton;          // 报警后隐藏
    [SerializeField] private ElectricalPanel electricalPanel;  // 断电后隐藏
    [SerializeField] private ExtinguisherCabinet cabinet;      // 取用后隐藏
    [SerializeField] private WetTowel towel;                    // 拾取后隐藏
    [SerializeField] private InteractableDoor door;             // 开启后隐藏

    [Header("参数")]
    [SerializeField] private float visibleRange = 30f;
    [SerializeField] private float bobAmplitude = 0.05f;
    [SerializeField] private float bobSpeed = 2.2f;
    [SerializeField] private float pulseSpeed = 3f;

    private Transform visual;
    private Transform player;
    private Vector3 anchor;
    private Vector3 iconBaseScale;
    private float seed;

    private void Awake()
    {
        visual = transform.Find("Visual");
        anchor = transform.position;
        seed = Random.value * 10f;
        var icon = transform.Find("Visual/Icon");
        if (icon != null) iconBaseScale = icon.localScale;
    }

    private void Update()
    {
        // 完成判定：隐藏整个标记
        bool done = (alarmButton != null && !alarmButton.CanInteract)
                 || (electricalPanel != null && !electricalPanel.CanInteract)
                 || (cabinet != null && !cabinet.CanInteract)
                 || (door != null && door.IsOpen)
                 || (towel != null && towel.GetComponent<WetTowel>() == null);
        if (done) { gameObject.SetActive(false); return; }

        if (player == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) player = p.transform;
        }
        if (player == null) return;

        // 距离显隐（近处才显示，避免远处杂乱）
        float d = Vector3.Distance(player.position, anchor);
        if (visual != null) visual.gameObject.SetActive(d <= visibleRange);

        // 上下浮动
        transform.position = anchor + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + seed) * bobAmplitude);

        // 图标脉冲
        var icon = transform.Find("Visual/Icon");
        if (icon != null && iconBaseScale != Vector3.zero)
            icon.localScale = iconBaseScale * (1f + 0.12f * Mathf.Sin(Time.time * pulseSpeed + seed));

        // 公告板朝向玩家相机（只绕Y轴）
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 to = cam.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }
    }
}
