using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeteksiMesinPress : MonoBehaviour
{
    void Start()
    {
        
    }
    private void OnTriggerEnter(Collider other)
    {


        if (other.gameObject.layer == 6)
        {
            transform.GetComponentInParent<MesinPress>().TriggerKecelakaan();

            other.transform.parent.gameObject.SetActive(false);

        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
