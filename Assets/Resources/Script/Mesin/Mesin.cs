using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mesin : MonoBehaviour
{


    protected bool isMesinOn;

    [Header("Info Mesin")]
    public string namaMesin = "Mesin Press";

    [Header("Efek Visual , Suara & Mesin")]
    [SerializeField] protected AudioSource SuaraMesin;

    [SerializeField] protected Animator Anim;



    protected virtual void NyalakanMesin()
    {
       
    }
    protected virtual void MatikanMesin()
    {
       
    }




}
