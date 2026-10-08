using UnityEngine;

/// <summary>
/// 第一人称手部动画：走路晃动（与 FPC bob 同步）+ 待机呼吸微动 + 交互伸手。
/// 挂在 Player 根物体上，引用两个手部 Transform（相机子物体）。
/// </summary>
public class HandSway : MonoBehaviour
{
    [SerializeField] private Transform handL;
    [SerializeField] private Transform handR;
    [SerializeField] private FirstPersonController fpc;

    [Header("晃动参数")]
    [SerializeField] private float swayAmount = 0.006f;
    [SerializeField] private float swaySpeed = 2f;

    [Header("交互伸手")]
    [SerializeField] private float reachDistance = 0.12f;
    [SerializeField] private float reachDuration = 0.25f;

    private Vector3 handL_base;
    private Vector3 handR_base;
    private float reachTimer;
    private bool isReaching;

    private void Start()
    {
        if (fpc == null) fpc = GetComponent<FirstPersonController>();
        if (handL != null) handL_base = handL.localPosition;
        if (handR != null) handR_base = handR.localPosition;
    }

    private void Update()
    {
        if (Time.timeScale <= 0.01f) return;
        UpdateSway();
        UpdateReach();
    }

    private void UpdateSway()
    {
        if (fpc == null) return;
        float bobT = fpc.BobTimer;
        float speedRatio = fpc.IsSprinting ? 1.4f : fpc.IsCrouching ? 0.5f : 1f;
        float amp = Mathf.Sin(bobT) * swayAmount * speedRatio;
        float breathe = Mathf.Sin(Time.time * swaySpeed) * 0.004f;

        if (handL != null)
        {
            var lp = handL.localPosition;
            handL.localPosition = new Vector3(
                handL_base.x - amp * 0.6f,
                handL_base.y + breathe - amp * 0.4f,
                handL_base.z);
        }
        if (handR != null)
        {
            var lp = handR.localPosition;
            handR.localPosition = new Vector3(
                handR_base.x + amp * 0.6f,
                handR_base.y + breathe - amp * 0.4f,
                handR_base.z);
        }
    }

    private void UpdateReach()
    {
        if (!isReaching) return;
        reachTimer += Time.deltaTime;
        if (reachTimer >= reachDuration) isReaching = false;
    }

    /// <summary>按 E 交互时调用——手短暂向前伸。</summary>
    public void TriggerReach()
    {
        isReaching = true;
        reachTimer = 0f;
    }

    /// <summary>每帧获取伸手偏移量（供外部手部 Transform 使用）。</summary>
    public float ReachOffset => isReaching ? Mathf.Sin(reachTimer / reachDuration * Mathf.PI) * reachDistance : 0f;
}
