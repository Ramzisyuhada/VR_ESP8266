using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeteksiMesinPress : MonoBehaviour
{

    private Connection Con;
    
    void Start()
    {
        Con = GetComponent<Connection>();
    }

    public GameObject tangan;

    public void TanganAktif()
    {
        if (tangan != null)
        {
            tangan.GetComponentInChildren<SkinnedMeshRenderer>().enabled = true;
        }
        else
        {
            Debug.LogWarning("Tangan belum terdeteksi.");
        }
    }

    private void OnApplicationQuit()
    {
        Con.SendWebSocketMessage("OFF");

    }

    private void OnTriggerEnter(Collider other)
    {


        if (other.gameObject.layer == 6)
        {
            Con.SendWebSocketMessage("PING");
            transform.GetComponentInParent<MesinPress>().TriggerKecelakaan();
            Transform current = other.transform;
            while (current != null && current.name != "LeftHandModel" && current.name != "RightHandModel")
            {
                current = current.parent;
            }
            if (current != null)
            {
                 tangan = current.gameObject;
                tangan.GetComponentInChildren<SkinnedMeshRenderer>().enabled = false;
                Debug.Log("Dapat tangan: " + tangan.name);
                tangan.SetActive(false);

            }

        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == 6)
        {
           // transform.GetComponentInParent<MesinPress>().TriggerKecelakaan();

            other.transform.parent.gameObject.SetActive(true);

        }
    }
    // Update is called once per frame
    void Update()
    {
       // Con.SendWebSocketMessage();

    }
}
