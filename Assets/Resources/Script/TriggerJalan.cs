using HurricaneVR.Framework.Components;
using HurricaneVR.Framework.Core.Grabbers;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TriggerJalan : HVRImpactHapticsBase
{
    [Header("Hurricane VR")]
    [Tooltip("Referensi Hand Grabber Kiri.")]
    public HVRHandGrabber HandLeft;

    [Tooltip("Referensi Hand Grabber Kanan.")]
    public HVRHandGrabber HandRight;

    [Tooltip("Preset haptics (Duration / Frequency / MaxForce).")]
    public HVRImpactHaptics Data;

    public override float MaxForce => Data ? Data.MaxForce : 1f;

    [Header("Filter Trigger")]
    public string TriggerTag = "OutZone";
    public LayerMask TriggerLayers = 0;

    [Header("UI")]
    [SerializeField] private GameObject ui;

    [Header("Debug")]
    public bool DebugLogs = true;

    private Collider _col;

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    private void Awake()
    {
        _col = GetComponent<Collider>();
        if (_col && !_col.isTrigger)
        {
            _col.isTrigger = true;
            if (DebugLogs) Debug.LogWarning($"{name}: Collider di-set ke Trigger otomatis.");
        }

        if (!Data && DebugLogs)
            Debug.LogWarning($"{name}: HVRImpactHaptics (Data) belum diassign. Haptics akan pakai default.");
    }

    private void Start()
    {
        if (ui && ui.activeSelf)
            ui.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidTrigger(other)) return;
        Debug.Log(other.gameObject.tag);

        if (ui)
        {
            ui.SetActive(true);

            Transform cam = Camera.main.transform;
            float distance = 2f;

            // letakkan UI di depan kamera
            ui.transform.position = cam.position + cam.forward * distance;
            ui.transform.rotation = Quaternion.LookRotation(cam.forward, Vector3.up);
        }

        VibrateHand(HandLeft);
        VibrateHand(HandRight);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsValidTrigger(other)) return;

        if (ui && ui.activeSelf)
            ui.SetActive(false);

        if (DebugLogs)
            Debug.Log($"{name}: Exit zona {TriggerTag}.");
    }

    private bool IsValidTrigger(Collider other)
    {
        if (!other) return false;
        if (!string.IsNullOrEmpty(TriggerTag) && !other.CompareTag(TriggerTag))
            return false;

        if (TriggerLayers.value != 0)
        {
            int layerMask = 1 << other.gameObject.layer;
            if ((TriggerLayers.value & layerMask) == 0)
                return false;
        }

        return true;
    }

    private void VibrateHand(HVRHandGrabber hand)
    {
        if (hand && hand.Controller)
        {
            float duration = Data ? Data.Duration : 0.08f;
            float frequency = Data ? Data.Frequency : 150f;
            hand.Controller.Vibrate(duration, 1f, frequency);

            if (DebugLogs)
                Debug.Log($"{name}: Vibrate {hand.name} -> duration={duration}, freq={frequency}");
        }
    }
}
