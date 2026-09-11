using UnityEngine;

/// <summary>胜利触发器：玩家到达楼梯间安全出口即通关。</summary>
[RequireComponent(typeof(BoxCollider))]
public class WinTrigger : MonoBehaviour
{
    [SerializeField] private ResultPanelController resultPanel;
    [SerializeField] private GameHUD hud;
    [SerializeField, TextArea(3, 8)] private string knowledgeDetailOverride;

    private bool won;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (won) return;
        if (other.GetComponentInParent<FirstPersonController>() == null) return;

        won = true;
        float t = hud != null ? hud.ElapsedSeconds : 0f;
        int total = (int)t;
        string detail = !string.IsNullOrEmpty(knowledgeDetailOverride) ? knowledgeDetailOverride :
            $"逃生用时 {total / 60:00}:{total % 60:00}\n\n" +
            "本关学到的逃生知识：\n" +
            "· 电气起火：先断电，再用灭火器扑救\n" +
            "· 灭火器使用：对准火焰根部持续喷射\n" +
            "· 浓烟是火场第一杀手：低姿前行 + 湿毛巾捂口鼻\n" +
            "· 沿疏散指示标志撤离，不贪恋财物、不回头";
        resultPanel.ShowWin(detail);
    }
}
