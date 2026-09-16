using UnityEngine;

/// <summary>交互提示浮动文字的呼吸动画（上下浮动+始终面向相机）。</summary>
public class PhoneHintBob : MonoBehaviour
{
    [SerializeField] private float bobAmplitude = 0.05f;
    [SerializeField] private float bobSpeed = 2.2f;

    private Vector3 basePos;

    private void Start()
    {
        basePos = transform.localPosition;
    }

    private void Update()
    {
        transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmplitude);
        if (Camera.main != null)
        {
            var cam = Camera.main.transform;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.position);
        }
    }
}
