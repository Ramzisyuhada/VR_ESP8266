using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPC_MP1_Controller : MonoBehaviour
{


    [SerializeField] MesinPress _MesinPress;
    [SerializeField] AudioSource _AudioSource;
    public void KondisiNyalakanMesin()
    {
        _MesinPress.OnTopPlateUpFinished();
        _AudioSource.Play();
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
