using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>游戏内 HUD：任务目标、用时计时、交互提示、受伤红晕、灭火器剂量、返回关卡选择。</summary>
public class GameHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Button backButton;

    [Header("M3 扩展")]
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Image vignetteImage;
    [SerializeField] private GameObject doseRoot;
    [SerializeField] private Image doseFillImage;
    [SerializeField] private PlayerInteraction player;

    public float ElapsedSeconds => Time.timeSinceLevelLoad;

    private void Awake()
    {
        backButton.onClick.AddListener(() => SceneLoader.Load(SceneNames.LevelSelect));
    }

    private void Update()
    {
        int total = (int)ElapsedSeconds;
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

