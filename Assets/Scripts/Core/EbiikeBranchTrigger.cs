using System;
using UnityEngine;

/// <summary>
/// 公寓双结局分支触发器：放在2F楼梯口，玩家进入时判断电动车火势决定A/B线。
/// A线（火小）→ 提示冲过 → 通往地面出口
/// B线（火大）→ 提示退守 → 退回安全户关门堵门缝窗口求救
/// </summary>
public class EbiikeBranchTrigger : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private FirePoint ebikeFire;
    [SerializeField] private float threshold = 0.55f;

    [Header("A线提示")]
    [TextArea(1, 3)] public string hintA = "烟不算太浓——低姿捂住口鼻，快速通过！";
    [Header("B线提示")]
    [TextArea(1, 3)] public string hintB = "电池燃烧产生剧毒烟气，不可强穿！立即退回房间固守待援！";

    public event Action<bool> OnBranch; // true=A线(可过) false=B线(退守)

    private bool fired;
    private TutorialUI tutorial;

    private void Start()
    {
        tutorial = FindFirstObjectByType<TutorialUI>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (fired) return;
        if (!other.CompareTag("Player")) return;
        if (ebikeFire == null) return;

        fired = true;
        bool canPass = ebikeFire.Intensity < threshold;
        if (tutorial != null) tutorial.ShowHint(canPass ? hintA : hintB);
        OnBranch?.Invoke(canPass);
    }

    /// <summary>测试/调试用重置。</summary>
    public void ResetTrigger() { fired = false; }
}
