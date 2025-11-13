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
    [Tooltip("Tag collider yang boleh memicu trigger.")]
    public string TriggerTag = "OutZone";

    [Tooltip("Layer yang boleh memicu trigger (0 = bebas).")]
    public LayerMask TriggerLayers = 0;

    [Header("UI")]
    [SerializeField] private GameObject ui;
    [Tooltip("Jarak UI dari kamera saat ditampilkan.")]
    public float UIDistance = 2f;

    [Header("Debug")]
    public bool DebugLogs = true;

    private Collider _col;
    private Camera _mainCam;

    [Header("WS")]
    public WsRouterClientNative Client;

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
            if (DebugLogs)
                Debug.LogWarning($"{name}: Collider di-set ke Trigger otomatis.");
        }

        if (!Data && DebugLogs)
            Debug.LogWarning($"{name}: HVRImpactHaptics (Data) belum diassign. Haptics akan pakai default.");
    }

    private void Start()
    {
        _mainCam = Camera.main;
        if (!_mainCam && DebugLogs)
            Debug.LogWarning($"{name}: Camera.main tidak ditemukan. UI tidak bisa di-posisikan di depan kamera.");

        if (ui && ui.activeSelf)
            ui.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidTrigger(other)) return;

        if (DebugLogs)
            Debug.Log($"{name}: Enter oleh {other.gameObject.name} (tag={other.tag}).");

        // Kirim status salah
        if (Client != null)
        {
            Client.KirimPesanKeClientTerpilih("SALAH");
        }
        else if (DebugLogs)
        {
            Debug.LogWarning($"{name}: Client WS belum diassign, tidak mengirim 'SALAH'.");
        }

        // Tampilkan UI di depan kamera
        if (ui)
        {
            ui.SetActive(true);
            PositionUIInFrontOfCamera();
        }

        // Getar kedua tangan
        VibrateHand(HandLeft);
        VibrateHand(HandRight);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsValidTrigger(other)) return;

        // Kirim status benar
        if (Client != null)
        {
            Client.KirimPesanKeClientTerpilih("BENAR");
        }
        else if (DebugLogs)
        {
            Debug.LogWarning($"{name}: Client WS belum diassign, tidak mengirim 'BENAR'.");
        }

        if (ui && ui.activeSelf)
            ui.SetActive(false);

        if (DebugLogs)
            Debug.Log($"{name}: Exit zona {TriggerTag} oleh {other.gameObject.name}.");
    }

    private bool IsValidTrigger(Collider other)
    {
        if (!other) return false;

        // Filter Tag
        if (!string.IsNullOrEmpty(TriggerTag) && !other.CompareTag(TriggerTag))
            return false;

        // Filter Layer
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
        if (!hand || !hand.Controller) return;

        float duration = Data ? Data.Duration : 0.08f;
        float frequency = Data ? Data.Frequency : 150f;
        float amplitude = 1f;

        hand.Controller.Vibrate(duration, amplitude, frequency);

        if (DebugLogs)
            Debug.Log($"{name}: Vibrate {hand.name} -> duration={duration}, freq={frequency}, amp={amplitude}");
    }

    private void PositionUIInFrontOfCamera()
    {
        if (!_mainCam || !ui) return;

        Transform cam = _mainCam.transform;
        Vector3 forward = cam.forward;
        forward.y = 0f; // optional: biar UI tidak miring ke atas/bawah terlalu ekstrem
        if (forward == Vector3.zero) forward = cam.forward;

        ui.transform.position = cam.position + forward.normalized * UIDistance;
        ui.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}
