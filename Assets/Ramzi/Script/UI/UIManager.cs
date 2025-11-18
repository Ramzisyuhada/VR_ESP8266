using HurricaneVR.Framework.Core.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject HalamanUIUtama;
    [SerializeField] private GameObject PilihanSimulasiUI;

    [SerializeField] private HVRPlayerController Player;

    private Vector3 PosisiPlayer;

    public WsRouterClientNative Server;
    public void MulaiGame()
    {

        Player.Mulai = true;
    }
    public void ExitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
        Application.Quit();

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
