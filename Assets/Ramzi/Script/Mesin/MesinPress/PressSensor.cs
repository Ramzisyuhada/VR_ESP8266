using UnityEngine;
using HurricaneVR.Framework.Core.HandPoser;

[RequireComponent(typeof(Collider))]
public class PressSensor : MonoBehaviour
{
    [Header("Refs (wajib)")]
    public MesinPress mesin;                          // drag MesinPress kamu

    [Header("Syarat Aman")]
    public int requiredKeySlots = 4;                  // minimal keyslot terpasang
    public string handTag = "Hand";                   // tag collider tangan

    [Header("Anti Nyenggol / Debounce")]
    public float minHoldTime = 0.25f;                 // wajib menyentuh sekian detik
    public float cooldownSec = 0.6f;                  // jeda antar-penekanan

    [HideInInspector] public bool _handInside = false; // tangan sedang berada di area
    private float _handEnterTime = -999f;              // waktu pertama kali masuk
    private float _nextAllowed = 0f;                   // cooldown timer
    private Collider _lastHand;                        // simpan collider tangan terakhir
    private bool IsPlay = false;

    // Simpan referensi tangan terakhir yang dinonaktifkan (untuk reset nanti)
    private GameObject _disabledHandRoot;

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (string.IsNullOrEmpty(handTag) || !other.CompareTag(handTag)) return;

        _handInside = true;
        _handEnterTime = Time.time;
        _lastHand = other;
    }

    private void OnTriggerExit(Collider other)
    {
        if (string.IsNullOrEmpty(handTag) || !other.CompareTag(handTag)) return;

        _handInside = false;
        _lastHand = null;
    }

    private void Update()
    {
        if (!mesin) return;
        if (!_handInside || !_lastHand) return;
        if (Time.time < _nextAllowed) return;
        if ((Time.time - _handEnterTime) < minHoldTime) return;
        if (IsPlay) return;

        // Semua syarat lolos → jalankan evaluasi
        TryEvaluate(_lastHand);
        IsPlay = true;
        _nextAllowed = Time.time + cooldownSec;
    }

    // ===================== LOGIKA INTI ======================

    private void TryEvaluate(Collider hand)
    {
        // Temukan grup tangan atas (yang punya HVRHandPoser)
        Transform handRoot = GetHandGroupTop(hand.transform);

        if (handRoot != null)
        {
            _disabledHandRoot = handRoot.gameObject;
            handRoot.gameObject.SetActive(false);
            Debug.Log($"[PressSensor] ❌ Menonaktifkan tangan: {handRoot.name}");
        }
        else
        {
            _disabledHandRoot = hand.gameObject;
            hand.gameObject.SetActive(false);
            Debug.LogWarning("[PressSensor] Hand root tidak ditemukan, fallback ke collider langsung.");
        }

        // Panggil mesin simulasi kecelakaan
        mesin.SimulasiKecelakaan();
    }

    /// <summary>
    /// Naik parent sampai menemukan HVRHandPoser (root tangan HVR).
    /// </summary>
    private Transform GetHandGroupTop(Transform t)
    {
        if (t == null) return null;

        Transform cur = t;

        while (cur != null)
        {
            // kalau sudah punya HVRHandPoser → berhenti
            if (cur.GetComponent<HVRHandPoser>() != null)
            {
                return cur;
            }

            // lanjut naik kalau masih dalam objek bertag Hand
            if (cur.CompareTag(handTag))
                cur = cur.parent;
            else
                break;
        }

        // fallback ke transform awal
        return t;
    }

    /// <summary>
    /// Reset sensor dan aktifkan kembali tangan yang sebelumnya dimatikan.
    /// </summary>
    public void ResetSensor()
    {
        Debug.Log("[PressSensor] 🔄 Reset sensor...");

        _handInside = false;
        _lastHand = null;
        _handEnterTime = -999f;
        _nextAllowed = 0f;
        IsPlay = false;

        // Hidupkan kembali tangan terakhir yang dimatikan
        if (_disabledHandRoot != null)
        {
            if (!_disabledHandRoot.activeSelf)
            {
                _disabledHandRoot.SetActive(true);
                Debug.Log($"[PressSensor] 🤚 Mengaktifkan kembali tangan: {_disabledHandRoot.name}");
            }

            _disabledHandRoot = null;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider>();
        if (c)
        {
            Gizmos.color = _handInside ? Color.red : Color.cyan;
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }
    }
#endif
}
