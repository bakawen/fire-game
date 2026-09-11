using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>教学提示与开场演出：阶段化引导文本（强科普植入）+ 开场黑幕淡出。文案按关卡序列化配置。</summary>
public class TutorialUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private Image blackOverlay;
    [SerializeField] private float hintHoldSeconds = 7.5f;

    [Header("关卡文案（每关配置）")]
    [SerializeField] private string wakeHint = "深夜 2:47，你被一阵刺鼻的烟味呛醒……";
    [SerializeField] private string controlsHint = "WASD 移动 · 鼠标转向 · 走近物体按 E 交互 · 按住 Ctrl 蹲下";
    [SerializeField] private string fireHint = "书桌上的插线板起火了！先按 E 拉下门边电闸断电，再按 E 取门后灭火器，对准火焰根部按住左键喷射";
    [SerializeField] private string extinguishedHint = "初期火已扑灭！但走廊浓烟弥漫——按 Ctrl 蹲下低姿前行，到东南角按 E 拿湿毛巾捂住口鼻";
    [SerializeField] private string blockHintText = "前方火势挡路！举起灭火器（按住左键）对准火焰根部，开辟逃生通道";

    [Header("事件源（场景配置）")]
    [SerializeField] private FirePoint deskFire;
    [SerializeField] private WetTowel towel;
    [SerializeField] private InteractableDoor roomDoor;
    [SerializeField] private FirePoint corridorBlockFire;
    [SerializeField] private Transform player;

    private float timer;
    private float holdTimer = -1f;
    private float overlayAlpha = 1f;
    private bool controlsShown;
    private bool fireHintShown;
    private bool blockHintShown;

    private void Start()
    {
        if (blackOverlay != null)
        {
            overlayAlpha = blackOverlay.color.a > 0f ? blackOverlay.color.a : 1f;
            blackOverlay.raycastTarget = false;
        }
        ShowHint(wakeHint);
    }

    private void OnEnable()
    {
        if (deskFire != null) deskFire.OnExtinguished += OnDeskFireOut;
        if (towel != null) towel.OnPickedUp += OnTowelPicked;
        if (roomDoor != null) roomDoor.OnOpened += OnDoorOpened;
    }

    private void OnDisable()
    {
        if (deskFire != null) deskFire.OnExtinguished -= OnDeskFireOut;
        if (towel != null) towel.OnPickedUp -= OnTowelPicked;
        if (roomDoor != null) roomDoor.OnOpened -= OnDoorOpened;
    }

    private void Update()
    {
        if (Time.timeScale <= 0.01f) return;
        timer += Time.deltaTime;

        // 开场黑幕淡出
        if (overlayAlpha > 0f && timer > 3.2f)
        {
            overlayAlpha = Mathf.Max(0f, overlayAlpha - Time.deltaTime / 1.8f);
            if (blackOverlay != null)
            {
                blackOverlay.color = new Color(0f, 0f, 0f, overlayAlpha);
                if (overlayAlpha <= 0f) blackOverlay.gameObject.SetActive(false);
            }
        }

        if (timer > 6f && !controlsShown)
        {
            controlsShown = true;
            ShowHint(controlsHint);
        }
        if (timer > 15f && !fireHintShown)
        {
            fireHintShown = true;
            ShowHint(fireHint);
        }

        // 提示自动隐藏
        if (holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;
            if (holdTimer <= 0f) HideHint();
        }

        // 走廊挡路火提示
        if (!blockHintShown && corridorBlockFire != null && corridorBlockFire.IsBurning && player != null)
        {
            float d = Vector3.Distance(player.position, corridorBlockFire.transform.position);
            if (d < 8f)
            {
                blockHintShown = true;
                ShowHint(blockHintText);
            }
        }
    }

    private void OnDeskFireOut(FirePoint fp)
    {
        fireHintShown = true;
        ShowHint(extinguishedHint);
    }

    private void OnTowelPicked()
    {
        ShowHint("湿毛巾已捂住口鼻，浓烟伤害大幅降低。按 E 开门，沿绿色疏散标志向楼梯间撤离！");
    }

    private void OnDoorOpened(InteractableDoor door)
    {
        ShowHint("火势正在蔓延，不要停留、不要回头拿财物！沿疏散指示向楼梯间方向撤离");
    }

    public void ShowHint(string text)
    {
        if (hintText == null) return;
        hintText.text = text;
        hintText.gameObject.SetActive(true);
        holdTimer = hintHoldSeconds;
    }

    private void HideHint()
    {
        holdTimer = -1f;
        if (hintText != null) hintText.gameObject.SetActive(false);
    }
}
