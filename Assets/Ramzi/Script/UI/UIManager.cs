using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject HalamanUIUtama;
    [SerializeField] private GameObject PilihanSimulasiUI;

    private Vector3 PosisiPlayer;

    public void MulaiGame()
    {
        HalamanUIUtama.SetActive(false);
    }
    public void ExitGame()
    {

    }
    public void RestartGame()
    {
        GameObject.FindWithTag("Player").transform.position = PosisiPlayer;
    }
    void Start()
    {
        PosisiPlayer = GameObject.FindWithTag("Player").transform.position;
        HalamanUIUtama.SetActive(true) ;
        PilihanSimulasiUI.SetActive(false) ;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
