using System.Collections;
using UnityEngine;

/// <summary>
/// 办公楼轰燃前兆警告（活火场版）：由 FireZoneGraph 的任一区火势越过临界（0.85）触发——
/// 中央红色大字 + 紧急警笛 + 屏幕震动 + 红晕。触发时机完全取决于玩家的处置：
/// 断电/初期压制/随手关门都会推迟它；拖延不处置，它就会提前到来。
/// </summary>
public class OfficeFlashover : MonoBehaviour
{
    [Header("警告文案")]
    [SerializeField, TextArea(2, 3)] private string warningText = "轰燃前兆！\n火势即将失控——立即撤离，不要停留！";

    [Header("引用")]
    [SerializeField] private FireZoneGraph graph;
    [SerializeField] private TutorialUI tutorial;
    [SerializeField] private GameHUD hud;
    [SerializeField] private AudioSource sirenAudio;
    [SerializeField] private AudioClip sirenClip;

    [Header("震屏")]
    [SerializeField] private float shakeSeconds = 0.5f;
    [SerializeField] private float shakeMagnitude = 0.045f;

    private bool fired;

    private void Start()
    {
        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialUI>();
        if (hud == null) hud = FindFirstObjectByType<GameHUD>();
        if (sirenAudio == null) sirenAudio = gameObject.AddComponent<AudioSource>();
        sirenAudio.playOnAwake = false;
        sirenAudio.loop = true;
        sirenAudio.spatialBlend = 0f;

        if (graph == null)
        {
            Debug.LogError("[OfficeFlashover] 未接 FireZoneGraph——活火场模式下必须由火场状态触发轰燃警告", this);
            return;
        }
        graph.OnZoneCritical += OnZoneCritical;
    }

    private void OnDestroy()
    {
        if (graph != null) graph.OnZoneCritical -= OnZoneCritical;
    }

    private void OnZoneCritical(int zoneIndex)
    {
        if (fired) return;
        fired = true;

        if (sirenAudio != null && sirenClip != null)
        {
            sirenAudio.clip = sirenClip;
            sirenAudio.Play();
        }
        if (tutorial != null)
        {
            tutorial.ShowHint(warningText);   // 二连发延长显示
            tutorial.ShowHint(warningText);
        }
        if (hud != null) hud.SetVignette(0.85f);
        StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        var cam = Camera.main;
        if (cam == null) yield break;
        var original = cam.transform.localPosition;
        float t = 0f;
        while (t < shakeSeconds)
        {
            t += Time.deltaTime;
            float fade = 1f - t / shakeSeconds;
            cam.transform.localPosition = original + new Vector3(
                (UnityEngine.Random.value - 0.5f) * 2f * shakeMagnitude * fade,
                (UnityEngine.Random.value - 0.5f) * 2f * shakeMagnitude * fade, 0f);
            yield return null;
        }
        cam.transform.localPosition = original;
    }
}
