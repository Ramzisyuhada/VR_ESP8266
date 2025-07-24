using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

public class MesinPress : MesinKecelakaanBase
{


    private Animator _Anim;
    [SerializeField] GameObject Tangan;
    [SerializeField] Transform posisi;
    [SerializeField] GameObject BesiPress;
    [SerializeField] AudioSource audio;
    [SerializeField] GameObject UI;
    private GameObject tangan;
    private bool Onmesin;

    private Connection con;
    public override void CekKondisi()
    {
    }
    public void Restart()
    {
        GetComponentInChildren<DeteksiMesinPress>().tangan.SetActive(true);

        UI.SetActive(false);
        _Anim.SetBool("Play", false);
        con.SendWebSocketMessage("OFF");
        Destroy(tangan);
        Onmesin = false;

    }
    public override void TriggerKecelakaan()
    {
        if (Onmesin)
        {
            UI.SetActive(true);    
            tangan = Instantiate(Tangan, posisi.position, Quaternion.identity);
            JalankanHidrolic();
            _Anim.SetBool("Play", true);
            audio.Play();
        }

    }
    public void JalankanHidrolic()
    {
        //BesiPress.SetActive(true);
        Onmesin = true;



    }

    
    void Start()
    {
        con = GetComponentInChildren<Connection>();
        _Anim = GetComponent<Animator>();   
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
