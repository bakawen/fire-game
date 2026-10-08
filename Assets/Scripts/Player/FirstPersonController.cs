using UnityEngine;

/// <summary>
/// 第一人称玩家控制器：WASD移动、Shift疾跑、鼠标视角、Ctrl/C蹲下、重力、
/// 头部晃动（Head Bob）、FOV速度/濒死反馈、准星渲染。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("移动")]
    [SerializeField] private float walkSpeed = 3.4f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float crouchSpeed = 1.7f;
    [SerializeField] private float gravity = -18f;

    [Header("濒死减速（PlayerHealth 设置）")]
    [HideInInspector] public float criticalSpeedFactor = 1f;

    [Header("视角")]
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private float mouseSensitivity = 2.2f;
    [SerializeField] private float pitchLimit = 85f;
    [SerializeField] private float normalFov = 70f;
    [SerializeField] private float sprintFovKick = 5f;
    [SerializeField] private float criticalFov = 58f;
    [SerializeField] private float fovLerpSpeed = 8f;

    [Header("蹲下")]
    [SerializeField] private float standHeight = 1.8f;
    [SerializeField] private float crouchHeight = 1.2f;
    [SerializeField] private float standCameraY = 1.62f;
    [SerializeField] private float crouchCameraY = 1.0f;
    [SerializeField] private float crouchLerpSpeed = 6f;

    [Header("头部晃动")]
    [SerializeField] private float bobAmplitude = 0.028f;
    [SerializeField] private float bobFrequency = 8.5f;

    public bool IsCrouching { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsGrounded => controller != null && controller.isGrounded;
    public float BobTimer => bobTimer;

    private CharacterController controller;
    private Camera cam;
    private float pitch;
    private float verticalVelocity;
    private float currentCameraY;
    private float bobTimer;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (GetComponent<PauseUI>() == null) gameObject.AddComponent<PauseUI>();
        if (GetComponent<HandSway>() == null) gameObject.AddComponent<HandSway>();
        if (cameraRoot == null)
        {
            var c = GetComponentInChildren<Camera>();
            if (c != null) cameraRoot = c.transform;
        }
        if (cameraRoot != null) cam = cameraRoot.GetComponent<Camera>();
        currentCameraY = standCameraY;
    }

    private void Start()
    {
        LockCursor();
    }

    private void Update()
    {
        HandleCursor();
        if (Time.timeScale <= 0.01f) return;
        HandleCrouch();
        HandleMovement();
        HandleLook();
        HandleFov();
    }

    private void HandleLook()
    {
        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
        transform.Rotate(0f, mx, 0f);
        pitch = Mathf.Clamp(pitch - my, -pitchLimit, pitchLimit);
        if (cameraRoot != null)
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleCrouch()
    {
        IsCrouching = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        float targetHeight = IsCrouching ? crouchHeight : standHeight;
        if (Mathf.Abs(controller.height - targetHeight) > 0.005f)
        {
            float h = Mathf.MoveTowards(controller.height, targetHeight, crouchLerpSpeed * Time.deltaTime);
            controller.height = h;
            controller.center = new Vector3(0f, h * 0.5f, 0f);
        }
    }

    private void HandleMovement()
    {
        float v = Input.GetAxisRaw("Vertical");
        float h = Input.GetAxisRaw("Horizontal");
        Vector3 input = transform.forward * v + transform.right * h;
        if (input.sqrMagnitude > 1f) input.Normalize();

        // 疾跑：Shift + 非蹲下 + 有前进输入
        IsSprinting = Input.GetKey(KeyCode.LeftShift) && !IsCrouching && v > 0.1f;

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        float speed = IsCrouching ? crouchSpeed
                    : IsSprinting ? walkSpeed * sprintMultiplier
                    : walkSpeed;
        speed *= criticalSpeedFactor;

        Vector3 velocity = input * speed + Vector3.up * verticalVelocity;
        if (velocity.sqrMagnitude > 0.01f)
            controller.Move(velocity * Time.deltaTime);

        // 蹲/站高度过渡
        float targetHeight = IsCrouching ? crouchHeight : standHeight;
        if (Mathf.Abs(controller.height - targetHeight) > 0.005f)
        {
            float h2 = Mathf.MoveTowards(controller.height, targetHeight, crouchLerpSpeed * Time.deltaTime);
            controller.height = h2;
            controller.center = new Vector3(0f, h2 * 0.5f, 0f);
        }

        // 相机高度过渡 + 头部晃动
        float targetCamY = IsCrouching ? crouchCameraY : standCameraY;
        if (cameraRoot != null)
        {
            currentCameraY = Mathf.MoveTowards(currentCameraY, targetCamY, crouchLerpSpeed * Time.deltaTime);

            float bobOffset = 0f;
            if (controller.isGrounded && input.sqrMagnitude > 0.1f)
            {
                float speedRatio = IsCrouching ? 0.5f : IsSprinting ? 1.4f : 1f;
                bobTimer += Time.deltaTime * bobFrequency * speedRatio;
                bobOffset = Mathf.Sin(bobTimer) * bobAmplitude * speedRatio;
            }
            else
            {
                // 静止时 bob 平滑归零
                bobTimer = Mathf.MoveTowards(bobTimer, 0f, Time.deltaTime * bobFrequency);
                bobOffset = Mathf.Sin(bobTimer) * bobAmplitude * 0.3f;
            }

            var lp = cameraRoot.localPosition;
            cameraRoot.localPosition = new Vector3(lp.x, currentCameraY + bobOffset, lp.z);
        }
    }

    private void HandleFov()
    {
        if (cam == null) return;
        float targetFov;
        if (criticalSpeedFactor < 1f)
            targetFov = criticalFov;
        else if (IsSprinting)
            targetFov = normalFov + sprintFovKick;
        else
            targetFov = normalFov;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, fovLerpSpeed * Time.deltaTime);
    }

    /// <summary>屏幕中心准星。</summary>
    private void OnGUI()
    {
        if (Time.timeScale <= 0.01f) return;
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        var prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.55f);
        GUI.DrawTexture(new Rect(cx - 1.5f, cy - 1.5f, 3f, 3f), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private void HandleCursor()
    {
        if (Time.timeScale <= 0.01f)
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }
        if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
            LockCursor();
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
