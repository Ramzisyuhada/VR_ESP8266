using System.Runtime.Remoting.Messaging;
using UnityEngine;

/// <summary>
/// Logika utama mesin GateCutting:
/// - State mesin (Nyala/BukaPintu/Cutting/Turun)
/// - Integrasi Platform (SnapOnReleaseByDistance) → PlatformSnapped
/// - Simulasi kecelakaan & aksi aman cek pelumasan
/// - Cek bahaya jari (opsional) pakai ambang jarak
/// </summary>
public class GateCutting : Mesin
{
    [Header("Audio")]
    [SerializeField] private AudioSource AudioBukaPintuSuara;
    [SerializeField] private AudioSource AudioCuttingSuara;
    [SerializeField] private AudioSource AudioAlarmKecelakaan; // opsional

    [Header("Deteksi Bahaya (opsional)")]
    [SerializeField] private Transform JarakCutting;      // titik bahaya (mata pisau)
    [SerializeField] private Transform JarakJariKiri;     // referensi jari kiri
    [SerializeField] private Transform JarakJariKanan;    // referensi jari kanan
    [SerializeField] private float ambangJarakBahaya = 0.05f; // meter

    [Header("Platform (benda aman untuk cek pelumasan)")]
    [SerializeField] private SnapOnReleaseByDistance platform; // drag komponen snap milik Platform
    private bool _platformSnapped;

    public bool PlatformSnapped => platform && platform.IsSnapped;

    private enum StatetMesin { None, BukaPintu, Nyala, Cutting, Turun }
    [SerializeField] private StatetMesin _statetMesin = StatetMesin.None;

    [Header("VFX & Audio")]
    [SerializeField] private GameObject darahVFXObj;
    [SerializeField] private AudioSource darahSFX;     // opsional suara cipratan
    [SerializeField] private AudioSource audioAman;
    [SerializeField] private AudioSource audioSalah;

    [Header("UI Aman")]
    [SerializeField] private GameObject uiAman;        // drag image atau panel “Aman”
    [SerializeField] private float uiAmanDuration = 2f; // durasi tampil (detik)

    // ===== Unity lifecycle =====

    public bool SimulasiBenar;
    private Vector3 _platformStartPos;
    private Quaternion _platformStartRot;
    private Transform _platformStartParent;
    private void ResetPlatformTransform()
    {
        if (!platform) return;

        var t = platform.transform;

        // 1) Kalau lagi di-pegangan Hurricane VR → lepas paksa
        //    (opsional; abaikan jika tidak pakai HVR)
#if HVR_DEFINED
    var grab = t.GetComponent<HurricaneVR.Framework.Core.HVRGrabbable>();
    if (grab)
    {
        // ForceRelease() umumnya tersedia di HVRGrabbable
        // Jika namanya berbeda di versimu, ganti sesuai API
        try { grab.ForceRelease(); } catch { }
    }
#endif

        // 2) Kalau script SnapOnReleaseByDistance punya API untuk unsnap, panggil
        //    (contoh nama metode, sesuaikan dengan punyamu)
        try
        {
            // Misal: platform.ForceUnsnapped(); atau platform.UnsnapNow();
            // Jika tidak ada, tidak masalah—kita reset transform saja.
            var mi = platform.GetType().GetMethod("ForceUnsnapped");
            if (mi != null) mi.Invoke(platform, null);
        }
        catch { /* aman di-skip */ }

        // 3) Matikan gerak fisika sebelum dipindah
        var rb = t.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        // 4) Kembalikan parent, posisi, rotasi awal
        t.SetParent(_platformStartParent, true);
        t.SetPositionAndRotation(_platformStartPos, _platformStartRot);

        // 5) Flag internal
        _platformSnapped = false;

        // 6) (Opsional) refresh preview ghost kalau ada metodenya
        try
        {
            var mi = platform.GetType().GetMethod("RefreshPreview");
            if (mi != null) mi.Invoke(platform, null);
        }
        catch { }

        Debug.Log("[GateCutting] Platform direset ke posisi awal.");
    }
    private void OnEnable()
    {
        if (platform)
        {
            platform.OnSnapped += OnPlatformSnapped;
            platform.OnUnsnapped += OnPlatformUnsnapped;
        }
    }

    private void OnDisable()
    {
        if (platform)
        {
            platform.OnSnapped -= OnPlatformSnapped;
            platform.OnUnsnapped -= OnPlatformUnsnapped;
        }
    }

    // ===== Platform events =====
    private void OnPlatformSnapped(Transform t)
    {
        _platformSnapped = true;
        SimulasiBenar = true;
        Debug.Log("[GateCutting] Platform SNAPPED di lokasi aman.");
        // Jika ingin auto mulai prosedur aman saat platform dipasang DAN mesin sedang aktif:
        // if (SawAktif()) AksiBenar_CekPelumasan();
    }

    private void OnPlatformUnsnapped()
    {
        _platformSnapped = false;
        Debug.Log("[GateCutting] Platform dilepas (UNSNAPPED).");
    }

    public void SimulasiKecelakaan(Vector3 hitPos, Transform parent = null)
    {
        if (SawAktif())
        {
            Pemberitahuan.SetActive(true);

            SimulasiBenar = false;
            HeaderText.text = "Simulasi Gagal! Anda mengalami kecelakaan.";
            if (audioSalah != null) audioSalah.Play();
            
            Debug.LogWarning("[GateCutting] 🚨 SIMULASI KECELAKAAN DI " + hitPos);

            if (darahVFXObj)
            {
                GameObject fx = Instantiate(darahVFXObj, hitPos, Quaternion.identity);
                fx.transform.SetParent(parent,true);
                var ps = fx.GetComponent<ParticleSystem>();
                
                if (ps) ps.Play();
                Destroy(fx, ps ? ps.main.duration + 1f : 2f);
            }


            if (darahSFX)
                darahSFX.Play();
            wsRouter.KirimPesanKeClientTerpilih("TEST:ALL");
            // Opsional animasi darurat
            // Anim.SetTrigger("EmergencyStop");
        }
    }
    // ===== API publik =====
    public void OnMesin() {

        if (SuaraMesin != null && Anim != null && _statetMesin == StatetMesin.None)
        {
            _statetMesin = StatetMesin.Nyala;
            isMesinOn = true;
            SuaraMesin.Play();
        }
    }

    public void BukaPintu()
    {
        if (AudioBukaPintuSuara != null && _statetMesin == StatetMesin.Nyala)
        {
            _statetMesin = StatetMesin.BukaPintu;
            Anim.SetTrigger("BukaPintu");
            AudioBukaPintuSuara.Play();
        }
    }

    public void CuttingNyala()
    {
        if (AudioCuttingSuara != null && _statetMesin == StatetMesin.BukaPintu)
        {
            _statetMesin = StatetMesin.Cutting;
            AudioCuttingSuara.Play();
            Anim.SetTrigger("Cutting");
        }
    }

    public void Turun()
    {
        if (_statetMesin == StatetMesin.Cutting)
        {
            _statetMesin = StatetMesin.Turun;
            Anim.SetTrigger("Turun");
        }

        if (Pemberitahuan != null && HeaderText != null)
        {


            // Aktifkan UI pemberitahuan
            Pemberitahuan.SetActive(true);

            if (SimulasiBenar)
            {
                HeaderText.text = "Simulasi Benar! Selamat, Anda tidak mengalami kecelakaan.";
                if (audioAman != null) audioAman.Play();
            }
        }
        else
        {
            Debug.LogWarning("⚠️ HeaderText atau Pemberitahuan belum di-assign di Inspector!");
        }

    }
    private void HideUIAman()
    {
        if (uiAman)
            uiAman.SetActive(false);
    }
    /// <summary>
    /// Aksi BENAR: cek pelumasan aman saat platform dipakai mendekati cutting.
    /// Panggil dari sensor atau dari alurmu sendiri ketika platform sudah terpasang.
    /// </summary>
    public void AksiBenar_CekPelumasan()
    {
        if (PlatformSnapped && SawAktif())
        {
            if (audioAman)
                audioAman.Play();  // 🔊 mainkan suara aman

            if (uiAman)
            {
                uiAman.SetActive(true);
                CancelInvoke(nameof(HideUIAman));
                Invoke(nameof(HideUIAman), uiAmanDuration);
            }
        }
        else
        {
            Debug.Log("[GateCutting] ⏸ Tidak memenuhi syarat (PlatformSnapped & SawAktif) untuk cek pelumasan.");
        }
    }

    /// <summary>
    /// Aksi SALAH: tangan/jari mendekati area cutting saat berputar → kecelakaan.
    /// </summary>
    public void SimulasiKecelakaan()
    {
        if (SawAktif())
        {
            Debug.LogWarning("[GateCutting] 🚨 SIMULASI KECELAKAAN TERPICU!");
            // Hentikan mesin / anim:
            // Anim.SetTrigger("EmergencyStop");
            // Suara alarm:
            if (AudioAlarmKecelakaan) AudioAlarmKecelakaan.Play();
        }
    }

    // ===== Mesin base overrides =====
    protected override void NyalakanMesin()
    {
      
    }
    private Vector3 PosisiVelkFirst;
    private void Start()
    {
        if (platform)
        {
            _platformStartPos = platform.transform.position;
            _platformStartRot = platform.transform.rotation;
            _platformStartParent = platform.transform.parent;
        }
        else
        {
            Debug.LogWarning("[GateCutting] Platform belum di-assign di Inspector.");
        }
    }
    protected override void MatikanMesin()
    {
        if (SuaraMesin != null && Anim != null)
        {
            isMesinOn = false;
            SuaraMesin.Stop();
            Anim.Play(0);
            _statetMesin = StatetMesin.None;
        }
    }

    // ===== Helper =====
    private bool SawAktif() => _statetMesin == StatetMesin.Cutting || _statetMesin == StatetMesin.Turun;

    /// <summary>
    /// Opsional: jika ingin tambahan rule jari dekat area bahaya.
    /// </summary>
    private bool CekBahayaJari()
    {
        if (!JarakCutting)
        {
            Debug.LogWarning("[CekBahayaJari] ⚠️ Tidak ada JarakCutting — dianggap bahaya.");
            return true;
        }

        float jarakKiri = JarakJariKiri ? Vector3.Distance(JarakJariKiri.position, JarakCutting.position) : float.MaxValue;
        float jarakKanan = JarakJariKanan ? Vector3.Distance(JarakJariKanan.position, JarakCutting.position) : float.MaxValue;

        bool kiriBahaya = jarakKiri <= ambangJarakBahaya;
        bool kananBahaya = jarakKanan <= ambangJarakBahaya;

        Debug.Log($"[CekBahayaJari] Jkiri={jarakKiri:F3}m Jkanan={jarakKanan:F3}m Ambang={ambangJarakBahaya:F3} → Bahaya={kiriBahaya || kananBahaya}");
        return kiriBahaya || kananBahaya;
    }


    /// <summary>
    /// Reset seluruh simulasi ke kondisi awal (mesin mati, efek dan UI hilang).
    /// </summary>
    public void ResetSimulasi()
    {
        Debug.Log("[GateCutting] 🔄 Reset simulasi...");

        // 1️⃣ Hentikan semua suara
        if (SuaraMesin && SuaraMesin.isPlaying) SuaraMesin.Stop();
        if (AudioCuttingSuara && AudioCuttingSuara.isPlaying) AudioCuttingSuara.Stop();
        if (AudioBukaPintuSuara && AudioBukaPintuSuara.isPlaying) AudioBukaPintuSuara.Stop();
        if (AudioAlarmKecelakaan && AudioAlarmKecelakaan.isPlaying) AudioAlarmKecelakaan.Stop();
        if (darahSFX && darahSFX.isPlaying) darahSFX.Stop();
        if (audioAman && audioAman.isPlaying) audioAman.Stop();
        if (audioSalah && audioSalah.isPlaying) audioSalah.Stop();

        // 2️⃣ Hapus semua efek darah yang tersisa di scene
       
        
        // 3️⃣ Matikan UI aman jika aktif
        if (uiAman)
        {
            uiAman.SetActive(false);
            CancelInvoke(nameof(HideUIAman));
        }

        // 4️⃣ Reset flag dan status mesin
        _platformSnapped = false;
        SimulasiBenar = false;
        isMesinOn = false;
        _statetMesin = StatetMesin.None;

        // 5️⃣ Reset animator (jika ada)
        if (Anim)
        {
            Anim.Rebind();
            Anim.Update(0f);
        }
        ResetPlatformTransform();

        Debug.Log("[GateCutting] ✅ Simulasi telah direset.");
    }
}
