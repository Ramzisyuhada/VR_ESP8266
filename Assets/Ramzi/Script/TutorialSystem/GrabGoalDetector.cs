using System;
using UnityEngine;
using UnityEngine.Events;
using HurricaneVR.Framework.Components;   // HVRGrabbable
using HurricaneVR.Framework.Core;         // (namespace umum HVR)
using HurricaneVR.Framework.Core.Grabbers; // HVRGrabberBase

[DisallowMultipleComponent]
public class GrabGoalDetector : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Jika kosong, akan dicari HVRGrabbable pada GameObject ini.")]
    public HVRGrabbable Grabbable;

    public event Action OnGrab;                    // Untuk dipakai lewat kode
    public UnityEvent OnGrabUnity;                 // Supaya bisa hook di Inspector

    [Header("Options")]
    [Tooltip("Jika true, event ditembak hanya sekali lalu komponen akan disable.")]
    public bool FireOnceThenDisable = true;

    [Tooltip("Aktifkan log untuk debugging.")]
    public bool VerboseLog = true;

    private bool _fired;
    private UnityAction<HVRGrabberBase, HVRGrabbable> _handler; // simpan ref untuk unsubscribe aman

    private void Awake()
    {
        if (!Grabbable) Grabbable = GetComponent<HVRGrabbable>();
    }

    private void OnEnable()
    {
        if (!Grabbable)
        {
            if (VerboseLog) Debug.LogWarning("[GrabGoalDetector] Grabbable null. Komponen dimatikan.");
            enabled = false;
            return;
        }

        // Pastikan handler tunggal (hindari double subscribe jika enable/disable berkali-kali)
        _handler = OnGrabbedInternal;
        Grabbable.Grabbed.RemoveListener(_handler);
        Grabbable.Grabbed.AddListener(_handler);

        // Cek kondisi “sudah dipegang” lintas versi HVR
        try
        {
            // HVR 2.9.x
            var hands = Grabbable.HandGrabbers;
            if (hands != null && hands.Count > 0)
            {
                if (VerboseLog) Debug.Log($"[GrabGoalDetector] {Grabbable.name} sudah dipegang saat enable.");
                Fire();
                return;
            }
        }
        catch { /* abaikan kalau properti tidak ada di versi HVR tertentu */ }

        try
        {
            var t = Grabbable.GetType();
            var isHeldProp = t.GetProperty("IsBeingHeld") ?? t.GetProperty("IsGrabbed");
            if (isHeldProp != null)
            {
                var val = isHeldProp.GetValue(Grabbable, null);
                if (val is bool b && b)
                {
                    if (VerboseLog) Debug.Log($"[GrabGoalDetector] {Grabbable.name} terdeteksi dipegang (IsBeingHeld/IsGrabbed).");
                    Fire();
                    return;
                }
            }
        }
        catch { /* aman di-skip */ }

        _fired = false; // reset tiap enable
    }

    private void OnDisable()
    {
        // Unsubscribe aman
        if (Grabbable != null && _handler != null)
        {
            Grabbable.Grabbed.RemoveListener(_handler);
        }
    }

    private void OnDestroy()
    {
        // Safety net tambahan saat object dihancurkan
        if (Grabbable != null && _handler != null)
        {
            Grabbable.Grabbed.RemoveListener(_handler);
        }
    }

    private void OnGrabbedInternal(HVRGrabberBase grabber, HVRGrabbable grabbed)
    {
        if (VerboseLog) Debug.Log($"[GrabGoalDetector] Grabbed: {grabbed?.name} oleh {grabber?.name}");
        Fire();
    }

    private void Fire()
    {
        if (_fired) return;
        _fired = true;

        try { OnGrab?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        try { OnGrabUnity?.Invoke(); } catch (Exception e) { Debug.LogException(e); }

        if (FireOnceThenDisable) enabled = false;
    }
}
