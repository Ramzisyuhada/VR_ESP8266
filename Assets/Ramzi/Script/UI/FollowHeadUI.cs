using UnityEngine;

public class FollowHeadUI : MonoBehaviour
{
    [Header("Target (kamera/head)")]
    public Transform head; // drag MainCamera / VR Camera

    [Header("Posisi relatif")]
    public float distance = 1.0f;
    public float verticalOffset = -0.05f;
    public float lateralOffset = 0f;

    [Header("Smoothing")]
    public float posLerp = 12f;
    public float rotLerp = 12f;

    [Header("Orientasi")]
    public bool keepUpright = true;     // jaga agar tidak ikut roll
    public bool flipY180 = true;        // kalau UI terlihat kebalik, aktifkan ini

    void Reset() { if (!head && Camera.main) head = Camera.main.transform; }

    void LateUpdate()
    {
        if (!head) return;

        // Target posisi di depan kamera + offset
        Vector3 forward = head.forward;
        Vector3 right = head.right;

        Vector3 targetPos =
            head.position +
            forward * distance +
            head.up * verticalOffset +
            right * lateralOffset;

        // Lerp posisi
        transform.position = Vector3.Lerp(
            transform.position, targetPos,
            1f - Mathf.Exp(-posLerp * Time.deltaTime)
        );

        // Rotasi: hadapkan UI ke kamera (Z+ UI menunjuk ke kamera)
        Vector3 toCamera = head.position - transform.position;
        if (toCamera.sqrMagnitude < 0.0001f) return;

        Quaternion look = keepUpright
            ? Quaternion.LookRotation(toCamera, Vector3.up)
            : Quaternion.LookRotation(toCamera, head.up);

        // Banyak Canvas perlu dibalik 180° di Y agar sisi depan (Z+) menghadap kamera
        if (flipY180) look *= Quaternion.Euler(0f, 180f, 0f);

        transform.rotation = Quaternion.Slerp(
            transform.rotation, look,
            1f - Mathf.Exp(-rotLerp * Time.deltaTime)
        );
    }
}
