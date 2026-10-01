using System;
using UnityEngine;

/// <summary>胜利触发器：玩家到达楼梯间安全出口即通关。</summary>
[RequireComponent(typeof(BoxCollider))]
public class WinTrigger : MonoBehaviour
{
    [SerializeField] private ResultPanelController resultPanel;
    [SerializeField] private GameHUD hud;
    [SerializeField, TextArea(3, 8)] private string knowledgeDetailOverride;

    [Header("复盘尾部附加（办公楼时间轴复盘后追加；宿舍/公寓复盘为空不受影响）")]
    [SerializeField, TextArea(1, 2)] private string[] reviewTips =
    {
        "发现火情第一时间报警——警铃与广播是整栋楼的救命钟",
        "电气火灾先断电：断电即断它的燃料供给",
        "浓烟致命快于火焰：低姿、湿毛巾、随手关门",
        "疏散路线不是固定的：看指示、读火势，必要时果断折返",
        "初期火可扑救，越过初期立即撤——不恋战",
    };

    /// <summary>通关时触发（GuidanceChecklist 推进最终步骤）。</summary>
    public event Action OnWin;

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
        OnWin?.Invoke();
        float t = hud != null ? hud.ElapsedSeconds : 0f;
        int total = (int)t;
        string detail = !string.IsNullOrEmpty(knowledgeDetailOverride) ? knowledgeDetailOverride :
            $"逃生用时 {total / 60:00}:{total % 60:00}\n\n" +
            "本关学到的逃生知识：\n" +
            "· 电气起火：先断电，再用灭火器扑救\n" +
            "· 灭火器使用：对准火焰根部持续喷射\n" +
            "· 浓烟是火场第一杀手：低姿前行 + 湿毛巾捂口鼻\n" +
            "· 沿疏散指示标志撤离，不贪恋财物、不回头";
        // 疏散决策回顾（办公楼专用；宿舍/公寓账本为空自动跳过）
        string review = DecisionLedger.BuildReview();
        if (!string.IsNullOrEmpty(review))
        {
            detail = $"逃生用时 {total / 60:00}:{total % 60:00}\n\n{review}";
            // 尾部：一条今日要诀 + 知识考核引导（答题考核已从关卡中移到问答关）
            if (reviewTips != null && reviewTips.Length > 0)
                detail += "\n\n" + "【今日消防要诀】" + reviewTips[UnityEngine.Random.Range(0, reviewTips.Length)]
                    + "\n想检验学习成果？主菜单 → 消防问答";
        }
        resultPanel.ShowWin(detail);
    }
}
