using UnityEngine;
using UnityEngine.UI;

/// <summary>关卡选择界面：宿舍 / 办公楼 / 公寓逃生 + 返回主菜单。</summary>
public class LevelSelectUI : MonoBehaviour
{
    [SerializeField] private Button dormButton;
    [SerializeField] private Button classroomButton;
    [SerializeField] private Button apartmentButton;
    [SerializeField] private Button backButton;

    private void Awake()
    {
        dormButton.onClick.AddListener(() => SceneLoader.Load(SceneNames.Dorm));
        classroomButton.onClick.AddListener(() => SceneLoader.Load(SceneNames.Office));
        apartmentButton.onClick.AddListener(() => SceneLoader.Load(SceneNames.Apartment));
        backButton.onClick.AddListener(() => SceneLoader.Load(SceneNames.MainMenu));
    }

    private void Start()
    {
        // 菜单界面必须释放鼠标（可能从锁定状态的场景切换而来）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
