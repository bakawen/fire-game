using UnityEngine;

/// <summary>
/// 电梯门动画事件接收者：ElevatorDoors_open剪辑内的AnimationEvent（开门音/关门音/关门定时）
/// 原由QA包Elevator.cs处理，该脚本已剥离；本组件接收事件并播放包自带门音效。
/// 关门定时逻辑由ElevatorTrap接管，此处为空实现防报错。仅04_Office使用。
/// </summary>
public class ElevatorDoorReceiver : MonoBehaviour
{
    [SerializeField] private AudioClip doorsOpenClip;
    [SerializeField] private AudioClip doorsCloseClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    private AudioSource audioSrc;

    private void Awake()
    {
        audioSrc = GetComponent<AudioSource>();
        if (audioSrc == null) audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
    }

    // AnimationEvent: 开门音效 @0.02s
    private void DoorsOpeningSoundPlay() => PlayOneShot(doorsOpenClip);

    // AnimationEvent: 关门音效 @1.32s
    private void DoorsClosingSoundPlay() => PlayOneShot(doorsCloseClip);

    // AnimationEvent: 原Elevator.cs的关门定时逻辑——已由ElevatorTrap接管，空实现
    private void DoorsClosingTimer() { }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip != null && audioSrc != null) audioSrc.PlayOneShot(clip, volume);
    }
}
