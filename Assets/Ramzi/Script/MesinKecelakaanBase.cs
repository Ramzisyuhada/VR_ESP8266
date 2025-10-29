using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MesinKecelakaanBase : MonoBehaviour, IMesinKecelakaan
{

    public string namaMesin;
    public abstract void CekKondisi();
    public abstract void TriggerKecelakaan();

    protected void Lapor(string pesan)
    {
        KecelakaanManager.Instance.TampilkanPesan($"[{namaMesin}] {pesan}");
    }
}

