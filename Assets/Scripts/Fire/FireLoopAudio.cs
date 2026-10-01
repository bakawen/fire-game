using UnityEngine;

/// <summary>
/// 火点环境音：spatial 循环播放火焰燃烧声，音量随火势强度、音高微随机。
/// 听觉也是火场信息——越靠近燃烧的区域，噼啪声越清晰。办公楼专用（场景接线挂到各 FirePoint）。
/// </summary>
[RequireComponent(typeof(FirePoint))]
public class FireLoopAudio : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [SerializeField] private float maxVolume = 0.55f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 30f;

    private FirePoint fp;
    private AudioSource src;

    private void Awake()
    {
        fp = GetComponent<FirePoint>();
        src = gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        src.pitch = Random.Range(0.92f, 1.08f);
        src.volume = 0f;
    }

    private void Update()
    {
        if (clip == null || !fp.IsBurning)
        {
            if (src.isPlaying) src.Stop();
            return;
        }
        if (!src.isPlaying) src.Play();
        src.volume = maxVolume * Mathf.Clamp01(fp.Intensity);
    }
}
