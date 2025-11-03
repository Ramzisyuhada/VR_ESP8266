using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CuttingProximitySensor : MonoBehaviour
{
    [Header("Refs (wajib)")]
    public GateCutting mesin;                        // drag GateCutting
    public SnapBase platform;         // drag komponen snap milik Platform

    [Header("Identifikasi")]
    public string handTag = "Hand";                  // tag collider tangan/jari
    public bool triggerOnEnterOnly = true;           // cek saat Enter saja (kurangi spam)
    public float cooldownSec = 0.2f;                 // debounce

    private float _nextAllowed;


    public bool CekOli;

    private void Reset()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnEnterOnly) Evaluate(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!triggerOnEnterOnly) Evaluate(other);
    }

    private void Evaluate(Collider other)
    {
        if (!mesin) return;
        if (Time.time < _nextAllowed) return;
        _nextAllowed = Time.time + cooldownSec;
        Vector3 hitPos = other.ClosestPoint(transform.position);

        // 1) Jika yang masuk adalah TANGAN/JARI
        if (!string.IsNullOrEmpty(handTag) && other.CompareTag(handTag))
        {
            //if (mesin.PlatformSnapped)
            //    mesin.AksiBenar_CekPelumasan();   // ✅ aman karena platform sudah terpasang
            //else
                mesin.SimulasiKecelakaan(hitPos, other.transform); // 👉 jadikan child sensor
            return;
        }else if (other.CompareTag("MasterPlat"))
        {
            CekOli = true;

            other.transform.GetChild(1).gameObject.SetActive(true);
            mesin.AksiBenar_CekPelumasan();
        }


        // 2) Jika yang masuk adalah PLATFORM (atau bagian-bagiannya) — biarkan saja
        if (platform && (other.gameObject == platform.gameObject || other.transform.IsChildOf(platform.transform)))
        {
            // Tidak perlu apa-apa: platform boleh mendekat
            return;
        }

        // 3) Objek lain: abaikan (atau anggap tidak aman jika ingin ketat)
        // mesin.SimulasiKecelakaan();
    }
}
