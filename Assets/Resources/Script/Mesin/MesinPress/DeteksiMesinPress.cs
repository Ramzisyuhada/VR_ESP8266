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
    private void OnTriggerEnter(Collider other)
    {


        if (other.gameObject.layer == 6)
        {
           // Con.SendWebSocketMessage();
            transform.GetComponentInParent<MesinPress>().TriggerKecelakaan();

            other.transform.parent.gameObject.SetActive(false);

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
