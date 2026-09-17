using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>游戏内 HUD：任务目标、用时计时、交互提示、受伤红晕、灭火器剂量。返回入口由 Esc 暂停面板承担。</summary>
public class GameHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text timerText;

    [Header("M3 扩展")]
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Image vignetteImage;
    [SerializeField] private GameObject doseRoot;
    [SerializeField] private Image doseFillImage;
    [SerializeField] private PlayerInteraction player;

    private float elapsed;   // 缩放时间累加：Esc 暂停（timeScale=0）时计时冻结，结算用时口径一致

    public float ElapsedSeconds => elapsed;

    private void Update()
    {
        if (Time.timeScale > 0.01f) elapsed += Time.deltaTime;
        int total = (int)elapsed;
        timerText.text = $"{total / 60:00}:{total % 60:00}";

        if (player != null)
        {
            string prompt = player.TargetPrompt;
            if (!string.IsNullOrEmpty(prompt))
            {
                promptText.text = prompt;
                promptText.gameObject.SetActive(true);
            }
            else
            {
                promptText.gameObject.SetActive(false);
            }

            FireExtinguisher extinguisher = player.HeldItem as FireExtinguisher;
            if (extinguisher != null)
            {
                doseRoot.SetActive(true);
                doseFillImage.fillAmount = extinguisher.Dose;
            }
            else
            {
                doseRoot.SetActive(false);
            }
        }
    }

    public void SetObjective(string text)
    {
        objectiveText.text = text;
    }

    /// <summary>受伤红晕（0=无伤，1=濒死）。</summary>
    public void SetVignette(float alpha)
    {
        if (vignetteImage == null) return;
        Color c = vignetteImage.color;
        c.a = Mathf.Clamp01(alpha);
        vignetteImage.color = c;
    }
}

