using UnityEngine;

/// <summary>随机出生点系统：每局从候选池随机选择出生位置，并通知火势系统按难度偏移时间表。</summary>
public class SpawnManager : MonoBehaviour
{
    [System.Serializable]
    public class SpawnPoint
    {
        [Tooltip("出生点标记名")]
        public string label;
        public Transform spawnTransform;
        [Tooltip("难度系数：越大火势越早失控（三层=1.6等）")]
        public float difficultyScale = 1f;
    }

    [SerializeField] private SpawnPoint[] spawnPoints;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private FireSpreadManager fireSpread;
    [SerializeField] private GameHUD hud;
    [SerializeField] private TutorialUI tutorial;

    public SpawnPoint Current { get; private set; }
    public int CurrentIndex { get; private set; }

    private void Start()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        int index = Random.Range(0, spawnPoints.Length);
        CurrentIndex = index;
        Current = spawnPoints[index];
        ApplySpawn();
    }

    private void ApplySpawn()
    {
        var player = GameObject.Find("Player");
        if (player != null && Current.spawnTransform != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = Current.spawnTransform.position;
            player.transform.rotation = Current.spawnTransform.rotation;
            if (cc != null) cc.enabled = true;
        }

        // 火势时间表按难度缩放：三层出生火势更早失控
        if (fireSpread != null && Current.difficultyScale != 1f)
            fireSpread.ApplyDifficultyScale(Current.difficultyScale);
    }
}
