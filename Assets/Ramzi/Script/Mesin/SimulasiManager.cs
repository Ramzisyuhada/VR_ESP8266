using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulasiManager : MonoBehaviour
{



    [Header("Kondisi Mesin Saat Play")]
     public  KondisiEnum kondisi;



    public static SimulasiManager Instance;
    private void Awake()
    {
        if (Instance != null && Instance == this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
    }



}
