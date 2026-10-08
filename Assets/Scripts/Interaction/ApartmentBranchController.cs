using UnityEngine;

/// <summary>
/// 公寓B线分支控制器：监听 EbiikeBranchTrigger.OnBranch 事件。
/// B线（火大不可过）→ 禁用1F的WinTrigger（阻止A线通关）、激活B_Line_Shelter交互。
/// A线（火小可过）→ 正常继续下楼。
/// 挂在场景中任意GO上。
/// </summary>
public class ApartmentBranchController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private EbiikeBranchTrigger branchTrigger;
    [SerializeField] private WinTrigger winTriggerA;   // 1F大厅原始出口
    [SerializeField] private WinTrigger winTriggerB;   // B线固守待援出口
    [SerializeField] private GameObject shelterGO;      // B_Line_Shelter GO

    private void Start()
    {
        if (branchTrigger == null)
            branchTrigger = FindFirstObjectByType<EbiikeBranchTrigger>();
        if (branchTrigger != null)
            branchTrigger.OnBranch += OnBranch;
    }

    private void OnBranch(bool canPass)
    {
        if (!canPass)
        {
            // B线：楼梯间不可通过 → 激活固守待援
            if (winTriggerA != null)
            {
                var col = winTriggerA.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
            if (shelterGO != null) shelterGO.SetActive(true);
        }
        // A线（canPass=true）→ 不做额外操作，玩家正常下楼
    }
}
