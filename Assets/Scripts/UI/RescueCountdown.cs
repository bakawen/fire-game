using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 救援倒计时（报警联动的核心回报）：按下手动报警后，右上角出现"消防救援预计到场 mm:ss"——
/// 全局压力时钟 + "119 已被消控室自动呼叫"的确定感；到 0 广播消防队到场。
/// 未报警永远看不到这行字（楼是哑的）——"先报警"由任务要求变成机制回报。
/// </summary>
public class RescueCountdown : MonoBehaviour
{
    [SerializeField] private AlarmButton alarmButton;
    [SerializeField] private TutorialUI tutorial;
    [Tooltip("报警到消防队到场的秒数")]
    [SerializeField] private float etaSeconds = 180f;
    [SerializeField, TextArea(1, 2)] private string arrivedBroadcast = "消防控制室广播：消防救援已到场，正在展开救援！";

    private TMP_Text label;
    private float remaining;
    private bool running;
    private bool arrived;

    private void Awake()
    {
        if (alarmButton == null) alarmButton = FindFirstObjectByType<AlarmButton>();
        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialUI>();
        if (alarmButton != null) alarmButton.OnAlarmTriggered += OnAlarm;
        BuildUI();
    }

    private void OnDestroy()
    {
        if (alarmButton != null) alarmButton.OnAlarmTriggered -= OnAlarm;
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("RescueCountdownCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var go = new GameObject("RescueLabel", typeof(RectTransform));
        go.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-26f, -64f);
        rt.sizeDelta = new Vector2(620f, 42f);
        label = go.AddComponent<TextMeshProUGUI>();
        label.fontSize = 26;
        label.color = new Color(0.96f, 0.72f, 0.32f, 0.95f);
        label.alignment = TextAlignmentOptions.Right;
        label.raycastTarget = false;
        label.text = "";
    }

    private void OnAlarm()
    {
        running = true;
        arrived = false;
        remaining = etaSeconds;
    }

    private void Update()
    {
        if (!running || label == null) return;
        if (Time.timeScale > 0.01f && !arrived) remaining -= Time.deltaTime;

        int total = Mathf.Max(0, (int)remaining);
        label.text = arrived
            ? "<color=#6FDE78>消防队已到场</color>"
            : $"消防救援预计到场  {total / 60:00}:{total % 60:00}";

        if (!arrived && remaining <= 0f)
        {
            arrived = true;
            if (tutorial != null) tutorial.ShowHint(arrivedBroadcast);
            StartCoroutine(FadeOutRoutine());
        }
    }

    private IEnumerator FadeOutRoutine()
    {
        float t = 0f;
        while (t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
        if (label != null) label.text = "";
        running = false;
    }
}
