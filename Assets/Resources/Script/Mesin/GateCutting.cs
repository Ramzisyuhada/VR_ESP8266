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

    [Header("UI Aman")]
    [SerializeField] private GameObject uiAman;        // drag image atau panel “Aman”
    [SerializeField] private float uiAmanDuration = 2f; // durasi tampil (detik)

    // ===== Unity lifecycle =====
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
            Debug.LogWarning("[GateCutting] 🚨 SIMULASI KECELAKAAN DI " + hitPos);

            // 🔴 Efek darah di posisi trigger
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

            // Opsional animasi darurat
            // Anim.SetTrigger("EmergencyStop");
        }
    }
    // ===== API publik =====
    public void OnMesin() => NyalakanMesin();

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
        if (SuaraMesin != null && Anim != null && _statetMesin == StatetMesin.None)
        {
            _statetMesin = StatetMesin.Nyala;
            isMesinOn = true;
            SuaraMesin.Play();
            Anim.Play(1);
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
}
