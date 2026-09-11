using UnityEngine;

/// <summary>第一人称玩家控制器：WASD移动、鼠标视角、Ctrl/C蹲下、重力。</summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("移动")]
    [SerializeField] private float walkSpeed = 3.4f;
    [SerializeField] private float crouchSpeed = 1.7f;
    [SerializeField] private float gravity = -18f;

    [Header("视角")]
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private float mouseSensitivity = 2.2f;
    [SerializeField] private float pitchLimit = 85f;

    [Header("蹲下")]
    [SerializeField] private float standHeight = 1.8f;
    [SerializeField] private float crouchHeight = 1.2f;
    [SerializeField] private float standCameraY = 1.62f;
    [SerializeField] private float crouchCameraY = 1.0f;
    [SerializeField] private float crouchLerpSpeed = 6f;

    public bool IsCrouching { get; private set; }

    private CharacterController controller;
    private float pitch;
    private float verticalVelocity;
    private float currentCameraY;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraRoot == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraRoot = cam.transform;
        }
        currentCameraY = standCameraY;
    }

    private void Start()
    {
        LockCursor();
    }

    private void Update()
    {
        // 光标管理必须最先执行：结算面板（timeScale=0）时也要能解锁光标供点击
        HandleCursor();

        if (Time.timeScale <= 0.01f) return;

        HandleCrouch();
        HandleMovement();
        HandleLook();
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

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        float speed = IsCrouching ? crouchSpeed : walkSpeed;
        Vector3 velocity = input * speed + Vector3.up * verticalVelocity;
        // 仅在有实际位移时调用 Move（避免纯旋转时的碰撞解算把玩家推出）
        if (velocity.sqrMagnitude > 0.01f)
            controller.Move(velocity * Time.deltaTime);

        // 相机高度平滑过渡（蹲/站）
        float targetY = IsCrouching ? crouchCameraY : standCameraY;
        if (cameraRoot != null)
        {
            currentCameraY = Mathf.MoveTowards(currentCameraY, targetY, crouchLerpSpeed * Time.deltaTime);
            var lp = cameraRoot.localPosition;
            cameraRoot.localPosition = new Vector3(lp.x, currentCameraY, lp.z);
        }
    }

    private void HandleCursor()
    {
        // 结算面板显示中（游戏暂停）：解锁光标供点击面板按钮
        if (Time.timeScale <= 0.01f)
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }

        // Esc 解锁光标，让玩家可以点击 HUD 按钮（如返回关卡选择）
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        // 游戏进行中：点击画面重新锁定（例如按 Esc 后恢复）
        if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
            LockCursor();
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
