using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class MesinPress : Mesin
{



    [Header("VFX & Audio")]
    [SerializeField] private GameObject darahVFXObj;
    [SerializeField] private AudioSource darahSFX;     // opsional suara cipratan
    [SerializeField] private AudioSource audioAman;
    [SerializeField] private AudioSource AudioMesin;
    [SerializeField] private AudioSource AudioAlarmKecelakaan;

    private enum StatetMesin { None, TopPlateUp, Nyala, CylinderForward }
    [SerializeField] private StatetMesin _statetMesin = StatetMesin.None;


    [Header("Npc")]
    [SerializeField] Transform PosisiNpc;
    private Animator AnimNpc;
    [Header("Player")]
    private Vector3 PosisiPlayer;
    private Animator Anim;

    private void Awake()
    {
        AnimNpc = PosisiNpc.GetComponent<Animator>();
        Anim = GetComponent<Animator>();
    }


    public void OnMesin()
    {
        

        
    }



    void Start()
    {
        
    }
    public void OnTopPlateUpFinished()
    {
        Debug.Log("Animasi TopPlateUp selesai ✅");
        if (AudioMesin != null)
        {
            AudioMesin.Play();
        }
    }

    void Update()
    {
        float JarakPlayerMesin2 = Vector3.Distance(GameObject.FindWithTag("Player").transform.position, transform.position);
        Debug.Log(JarakPlayerMesin2);
        if (_statetMesin== StatetMesin.None && JarakPlayerMesin2 < 2.5f) {
            Debug.Log("Hello ");
            _statetMesin = StatetMesin.Nyala;
            PosisiNpc.GetComponent<RigBuilder>().enabled = true;
            AnimNpc.SetTrigger("TopPlateUp");
           if (AudioMesin != null) AudioMesin.Play();

        }
    }
}
