using HurricaneVR.Framework.ControllerInput;
using HurricaneVR.Framework.Shared;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class MesinPress : Mesin
{
    [Header("VFX & Audio")]
    [SerializeField] private GameObject Tangan;
    [SerializeField] private AudioSource darahSFX;
    [SerializeField] private AudioSource audioAman;
    [SerializeField] private AudioSource AudioMesin;
    [SerializeField] private AudioSource AudioAlarmKecelakaan;
    [SerializeField] private AudioSource audioSalah;

    private enum StatetMesin { None, TopPlateUp, Nyala, CylinderForward, Simulasi }
    [SerializeField] private StatetMesin _statetMesin = StatetMesin.None;

    [Header("Npc")]
    [SerializeField] Transform PosisiNpc;
    private Animator AnimNpc;

    [Header("Player")]
    private Vector3 PosisiPlayer;
    private Animator Anim;

    [Header("Sensor")]
    [SerializeField] GameObject mesh;
    private PressSensor sensor;

    // Slot snap & objek sebelum dipasang ke slot
    public SnapOnReleaseByDistance[] KeySlot;
    public GameObject[] SebelumKeySlot; // objek kunci sebelum dipasang

    [Header("Input Controller")]
    public HVRGlobalInputs OculusInputMap;

    // Pose awal KeySlot
    private Vector3[] _keySlotStartPos;
    private Quaternion[] _keySlotStartRot;
    private Transform[] _keySlotParent;

    // Pose awal SebelumKeySlot
    private Vector3[] _keySebelumSlotStartPos;
    private Quaternion[] _keySebelumSlotStartRot;
    private Transform[] _SebelumkeySlotParent;

    // (opsional) freeze physics setelah reset
    [SerializeField] private float postResetFrozenSec = 0f;

    private void Awake()
    {
        if (mesh != null)
            sensor = mesh.GetComponent<PressSensor>();
        else
            Debug.LogWarning("[MesinPress] 'mesh' belum di-assign, sensor tidak diinisialisasi.");

        if (!PosisiNpc) Debug.LogWarning("[MesinPress] PosisiNpc belum di-assign.");
        AnimNpc = PosisiNpc ? PosisiNpc.GetComponent<Animator>() : null;

        Anim = GetComponent<Animator>();
    }

    private void Start()
    {
        // Simpan pose awal KeySlot
        if (KeySlot != null && KeySlot.Length > 0)
        {
            int len = KeySlot.Length;
            _keySlotStartPos = new Vector3[len];
            _keySlotStartRot = new Quaternion[len];
            _keySlotParent = new Transform[len];

            for (int i = 0; i < len; i++)
            {
                if (KeySlot[i] != null)
                {
                    _keySlotStartPos[i] = KeySlot[i].transform.position;
                    _keySlotStartRot[i] = KeySlot[i].transform.rotation;
                    _keySlotParent[i] = KeySlot[i].transform.parent;
                }
            }
        }

        // Simpan pose awal SebelumKeySlot (pakai array SebelumKeySlot, bukan KeySlot)
        // --- di Start() ---
        if (SebelumKeySlot != null && SebelumKeySlot.Length > 0)
        {
            int len = SebelumKeySlot.Length;
            _keySebelumSlotStartPos = new Vector3[len];
            _keySebelumSlotStartRot = new Quaternion[len];
            _SebelumkeySlotParent = new Transform[len];

            for (int i = 0; i < len; i++)
            {
                var obj = SebelumKeySlot[i];
                if (!obj) continue;

                _keySebelumSlotStartPos[i] = obj.transform.position;
                _keySebelumSlotStartRot[i] = obj.transform.rotation;
                _SebelumkeySlotParent[i] = obj.transform.parent;
            }
        }

    }

    public void OnMesin() { /* isi bila perlu */ }

    // NOTE: lebih natural mulai dari 0 agar perhitungan akurat
    int SlotKeys = 0;

    public void AksiBenar()
    {
        if (audioAman) audioAman.Play();
        if (HeaderText) HeaderText.text = "Simulasi Benar! Selamat, Anda tidak mengalami kecelakaan.";
        if (Pemberitahuan) Pemberitahuan.SetActive(true);
    }

    public void AksiSalah()
    {
        if (Tangan) Tangan.SetActive(true);
        if (audioSalah) audioSalah.Play();

        if (HeaderText) HeaderText.text = "Simulasi Gagal! Anda mengalami kecelakaan.";
        if (Pemberitahuan) Pemberitahuan.SetActive(true);

        if (darahSFX) darahSFX.Play();
    }

    public void SimulasiKecelakaan()
    {
        if (_statetMesin != StatetMesin.Simulasi) return;

        // hitung ulang jumlah key yang snapped SETIAP cek
        SlotKeys = 0;
        if (KeySlot != null)
        {
            foreach (var sn in KeySlot)
            {
                if (sn && sn.IsSnapped) SlotKeys++;
            }
        }

        // 4 slot sebagai ambang 'benar'. Jika kurang & tangan di dalam → salah.
        if (SlotKeys >= 4)
        {
            AksiBenar();
        }
        else
        {
            if ((sensor && sensor._handInside) || SlotKeys < 4)
                AksiSalah();
        }
    }

    public void CylinderForward()
    {
        if (AudioMesin != null && _statetMesin == StatetMesin.CylinderForward)
        {
            _statetMesin = StatetMesin.Simulasi;

            if (mesh) mesh.SetActive(true);
            AudioMesin.Play();
            Anim?.SetTrigger("CylinderBackward");
        }
    }

    public void OnTopPlateUpFinished()
    {
        Debug.Log("Animasi TopPlateUp selesai ✅");
        if (AudioMesin != null)
        {
            _statetMesin = StatetMesin.CylinderForward;
            AudioMesin.Play();
            // Catatan: sebelumnya kamu trigger "TopPlateUp" lagi.
            // Jika ini memang desain state-mu, biarkan. Kalau mau lanjut gerak silinder, pakai trigger yang sesuai.
            Anim?.SetTrigger("TopPlateUp");
        }
    }

    public void Mulai()
    {
        if (_statetMesin == StatetMesin.None)
        {
            _statetMesin = StatetMesin.Nyala;
            var rig = PosisiNpc ? PosisiNpc.GetComponent<RigBuilder>() : null;
            if (rig) rig.enabled = true;
            AnimNpc?.SetTrigger("TopPlateUp");
        }
    }

    void Update()
    {
        bool primaryPressed = OculusInputMap != null &&
                              (OculusInputMap.LeftPrimaryButtonState.Active || OculusInputMap.RightPrimaryButtonState.Active);

        if (primaryPressed && _statetMesin == StatetMesin.CylinderForward)
        {
            CylinderForward();
            SimulasiKecelakaan();
        }
    }

    public void ResetSimulasi()
    {
        Debug.Log("[MesinPress] 🔄 Reset simulasi...");

        // Reset sensor (aktifkan kembali tangan yang dimatikan)
        if (sensor) sensor.ResetSensor();

        // 1) Matikan semua audio
        if (AudioMesin && AudioMesin.isPlaying) AudioMesin.Stop();
        if (AudioAlarmKecelakaan && AudioAlarmKecelakaan.isPlaying) AudioAlarmKecelakaan.Stop();
        if (darahSFX && darahSFX.isPlaying) darahSFX.Stop();
        if (audioAman && audioAman.isPlaying) audioAman.Stop();
        if (audioSalah && audioSalah.isPlaying) audioSalah.Stop();

        // 2) Reset VFX/mesh & UI
        if (Tangan) Tangan.SetActive(false);
        if (mesh) mesh.SetActive(false);
        if (Pemberitahuan) Pemberitahuan.SetActive(false);

        // 3) Reset flag dan state
        SlotKeys = 0;
        _statetMesin = StatetMesin.None;

        // 4) Reset animator NPC dan mesin
        if (Anim) { Anim.Rebind(); Anim.Update(0f); }
        if (AnimNpc) { AnimNpc.Rebind(); AnimNpc.Update(0f); }

        // 5) UnSnap & reset posisi/rotasi semua KeySlot
        if (KeySlot != null)
        {
            for (int i = 0; i < KeySlot.Length; i++)
            {
                var slot = KeySlot[i];
                if (!slot) continue;

                // Coba panggil API unsnap jika ada
                try
                {
                    var mi = slot.GetType().GetMethod("ForceUnsnapped") ??
                             slot.GetType().GetMethod("UnsnapNow") ??
                             slot.GetType().GetMethod("ResetSnap");
                    if (mi != null) mi.Invoke(slot, null);
                }
                catch { /* aman di-skip */ }

                // Kembalikan transform slot
                if (_keySlotStartPos != null && i < _keySlotStartPos.Length)
                {
                    var t = slot.transform;
                    t.SetParent(_keySlotParent[i], true);
                    t.SetPositionAndRotation(_keySlotStartPos[i], _keySlotStartRot[i]);

                    var rb = t.GetComponent<Rigidbody>();
                    if (rb)
                    {
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.Sleep();
                    }
                }
            }
        }

        // 6) Reset posisi/rotasi objek "SebelumKeySlot" (kunci awal)
        // --- di ResetSimulasi(), setelah bagian KeySlot ---
        if (SebelumKeySlot != null && _keySebelumSlotStartPos != null)
        {
            for (int i = 0; i < SebelumKeySlot.Length; i++)
            {
                var obj = SebelumKeySlot[i];
                if (!obj) continue;

                var t = obj.transform;

                // kembalikan parent, posisi, rotasi awal
                if (_SebelumkeySlotParent != null && i < _SebelumkeySlotParent.Length)
                    t.SetParent(_SebelumkeySlotParent[i], true);

                if (i < _keySebelumSlotStartPos.Length && i < _keySebelumSlotStartRot.Length)
                    t.SetPositionAndRotation(_keySebelumSlotStartPos[i], _keySebelumSlotStartRot[i]);

                // rapikan rigidbody kalau ada
                var rb = t.GetComponent<Rigidbody>();
                if (rb)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.Sleep();
                }
            }
        }


        // 7) (opsional) freeze physics sementara
        if (postResetFrozenSec > 0f)
            StartCoroutine(CoFreezeRigidbodies(postResetFrozenSec));

        Debug.Log("[MesinPress] ✅ Semua keyslot & objek awal dikembalikan. Simulasi direset.");
    }

    private IEnumerator CoFreezeRigidbodies(float seconds)
    {
        var rbs = GetComponentsInChildren<Rigidbody>(true);
        var prev = new List<(Rigidbody rb, bool kinematic, RigidbodyConstraints cons)>(rbs.Length);
        foreach (var rb in rbs)
        {
            prev.Add((rb, rb.isKinematic, rb.constraints));
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        yield return new WaitForSeconds(seconds);

        foreach (var p in prev)
        {
            p.rb.isKinematic = p.kinematic;
            p.rb.constraints = p.cons;
        }
    }
}
