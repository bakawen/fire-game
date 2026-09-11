using UnityEngine;

/// <summary>锁闭的门：可交互提示但无法打开（其他宿舍门，营造环境氛围）。</summary>
public class LockedDoor : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "门已锁闭，室友们可能已撤离……";

    public string PromptText => prompt;
    public bool CanInteract => true;

    public void Interact(PlayerInteraction player)
    {
        // 锁闭门无操作
    }
}
