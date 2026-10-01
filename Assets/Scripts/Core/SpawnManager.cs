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
    [SerializeField] private FireZoneGraph zoneGraph;   // 办公楼活火场：难度作用于起源区增长
    [SerializeField] private GameHUD hud;
    [SerializeField] private TutorialUI tutorial;

    public SpawnPoint Current { get; private set; }
    public int CurrentIndex { get; private set; }

    private void Start()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;
        // 同局重试：死亡后"重新开始"——同一出生点重做同一道火场题
        int index;
        if (RetryRun.Active && RetryRun.SpawnIndex >= 0 && RetryRun.SpawnIndex < spawnPoints.Length)
            index = RetryRun.SpawnIndex;
        else
            index = Random.Range(0, spawnPoints.Length);
        RetryRun.SpawnIndex = index;
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

        // 火势难度按出生点缩放：旧管理器（时间表）/ 活火场图（起源增长）二选一接线
        if (fireSpread != null && Current.difficultyScale != 1f)
            fireSpread.ApplyDifficultyScale(Current.difficultyScale);
        if (zoneGraph != null && Current.difficultyScale != 1f)
            zoneGraph.SetDifficulty(Current.difficultyScale);
    }
}
