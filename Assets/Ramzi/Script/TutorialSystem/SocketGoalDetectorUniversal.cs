using System;
using UnityEngine;
using HurricaneVR.Framework.Components;
using HurricaneVR.Framework.Core.Grabbers;
using HurricaneVR.Framework.Core;

[AddComponentMenu("Tutorial/Socket Goal Detector Universal")]
public class SocketGoalDetectorUniversal : MonoBehaviour
{
    [Header("Target & Socket")]
    public HVRGrabbable Target;         // Objek yang harus ditaruh
    public Transform SocketRoot;        // Root transform socket (GameObject socket)
    public Transform SocketAnchor;      // Titik snap di socket (opsional)

    [Header("Fallback Check")]
    public float maxDistance = 0.07f;   // Maks jarak dari anchor
    public float maxAngle = 15f;        // Maks selisih orientasi
    public bool requireReleased = true; // Harus dilepas dari tangan?

    [Header("Lainnya")]
    public bool Once = true;            // Hanya sekali trigger?

    // Event untuk TutorialManager
    public event Action OnSocketed;

    // Runtime
    private bool done;
    private bool isHeld;
    private Rigidbody rb;

    void Awake()
    {
        if (Target) rb = Target.GetComponent<Rigidbody>();
        if (!SocketAnchor && SocketRoot) SocketAnchor = SocketRoot;
    }

    void OnEnable()
    {
        if (Target)
        {
            Target.Grabbed.AddListener(OnGrabbed);
            Target.Released.AddListener(OnReleased);
        }
    }

    void OnDisable()
    {
        if (Target)
        {
            Target.Grabbed.RemoveListener(OnGrabbed);
            Target.Released.RemoveListener(OnReleased);
        }
    }

    // Dipanggil dari UnityEvent socket jika ada
    public void OnSocketedEventBridge()
    {
        if (done && Once) return;
        if (IsChildOfSocket() || (IsNearAnchor() && IsOrientationClose()))
        {
            Complete();
        }
    }

    void Update()
    {
        if (done || !Target || !SocketRoot) return;
        if (requireReleased && isHeld) return;

        // 1) Jika parent sudah di socket
        if (IsChildOfSocket())
        {
            Complete();
            return;
        }

        // 2) Atau dekat anchor + orientasi cocok + hampir diam
        if (IsNearAnchor() && IsOrientationClose())
        {
            if (!rb || rb.IsSleeping() || rb.velocity.sqrMagnitude < 0.0025f)
                Complete();
        }
    }

    private void OnGrabbed(HVRGrabberBase g, HVRGrabbable grabbable) => isHeld = true;
    private void OnReleased(HVRGrabberBase g, HVRGrabbable grabbable) => isHeld = false;

    private bool IsChildOfSocket()
    {
        return Target && SocketRoot && Target.transform.IsChildOf(SocketRoot);
    }

    private bool IsNearAnchor()
    {
        return Target && SocketAnchor &&
               Vector3.Distance(Target.transform.position, SocketAnchor.position) <= maxDistance;
    }

    private bool IsOrientationClose()
    {
        return Target && SocketAnchor &&
               Quaternion.Angle(Target.transform.rotation, SocketAnchor.rotation) <= maxAngle;
    }

    private void Complete()
    {
        done = true;
        OnSocketed?.Invoke();
        if (Once) enabled = false;
    }
}
