using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MesinPress : MesinKecelakaanBase
{


    private Animator _Anim;

    [SerializeField] GameObject BesiPress;
    [SerializeField] GameObject ParticleDarah;
    public override void CekKondisi()
    {
    }

    public override void TriggerKecelakaan()

    {

        string status = Arduino.ReadSerialMessage();
        Vector3 posisi = BesiPress.transform.position;
        GameObject Darah = Instantiate(ParticleDarah, posisi, Quaternion.identity);
        Debug.Log("Memanggil TriggerKecelakaan()");

        Arduino.SendSerialMessage("Merah");
        if (status == SerialController.SERIAL_DEVICE_CONNECTED)
        {
            Debug.Log("Serial terhubung (baru saja)");
        }
        else if (status == SerialController.SERIAL_DEVICE_DISCONNECTED)
        {
            Debug.LogWarning("Serial terputus.");
            return;
        }
 

    }
    public void JalankanHidrolic()
    {
       //BesiPress.SetActive(true);
        _Anim.SetBool("Play", true);
       

    }

    
    void Start()
    {
        _Anim = GetComponent<Animator>();   
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
