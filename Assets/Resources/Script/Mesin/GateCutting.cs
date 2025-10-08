using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GateCutting : Mesin
{





    [SerializeField] AudioSource AudioBukaPintuSuara;
    [SerializeField] AudioSource AudioCuttingSuara;

    private enum StatetMesin
    {
        None,
        BukaPintu,
        Nyala,
        Cutting
    }


    private StatetMesin _statetMesin;



    public void CuttingNyala()
    {
        if (AudioCuttingSuara != null && _statetMesin == StatetMesin.BukaPintu) {
            AudioCuttingSuara.Play();
            Anim.SetTrigger("Cutting");

        }
    }
   
    public void BukaPintu()
    {
        if (AudioBukaPintuSuara != null && _statetMesin == StatetMesin.Nyala) {
            _statetMesin = StatetMesin.BukaPintu;
            Anim.SetTrigger("BukaPintu");
            AudioBukaPintuSuara.Play();
        }


    }
    public void OnMesin()
    {
        NyalakanMesin();
    }


    

    protected override void NyalakanMesin()
    {
        if(SuaraMesin != null && Anim != null && _statetMesin == StatetMesin.None) { 
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
        }
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
