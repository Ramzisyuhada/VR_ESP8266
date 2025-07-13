using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class KecelakaanManager : MonoBehaviour
{
    public static KecelakaanManager Instance;

    public SerialController serialController;

    [Header("Status Pemain")]
    public bool isMCBDimatikan;
    public bool isSarungTanganDipakai;
    public bool isHelmDipakai;
    public bool isEmergencyStopTekan;

    [Header("UI")]
    public TextMeshProUGUI infoText;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void TampilkanPesan(string pesan)
    {
        Debug.Log(pesan);
        if (infoText != null)
            infoText.text = pesan;
    }
}

