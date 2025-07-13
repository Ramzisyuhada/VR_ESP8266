using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class Detector : MonoBehaviour
{
    public XRBaseInteractor LeftController;   // Tangan kiri
    public XRBaseInteractor RightController;  // Tangan kanan
    public SerialController serialController;


    void OnEnable()
    {
        LeftController.selectEntered.AddListener(OnPegang);
        LeftController.selectExited.AddListener(OnLepas);

        RightController.selectEntered.AddListener(OnPegang);
        RightController.selectExited.AddListener(OnLepas);
    }

    void OnDisable()
    {
        LeftController.selectEntered.RemoveListener(OnPegang);
        LeftController.selectExited.RemoveListener(OnLepas);

        RightController.selectEntered.RemoveListener(OnPegang);
        RightController.selectExited.RemoveListener(OnLepas);
    }

    void OnPegang(SelectEnterEventArgs args)
    {
        var objek = args.interactableObject.transform.gameObject;
        string status = serialController.ReadSerialMessage();
        if (status == SerialController.SERIAL_DEVICE_CONNECTED)
        {
            Debug.Log("Serial terhubung (baru saja)");
        }
        else if (status == SerialController.SERIAL_DEVICE_DISCONNECTED)
        {
            Debug.LogWarning("Serial terputus.");
            return;
        }

        if (objek.CompareTag("Merah")) {
            serialController.SendSerialMessage("Merah");

        }
        else if (objek.CompareTag("Kuning"))
        {
            serialController.SendSerialMessage("Kuning");

        }
        else if (objek.CompareTag("Hijau")){
            serialController.SendSerialMessage("Hijau");

        }

    }

    void OnLepas(SelectExitEventArgs args)
    {
        var objek = args.interactableObject.transform.gameObject;
    }

    private void Update()
    {
        string response = serialController.ReadSerialMessage();
        if (!string.IsNullOrEmpty(response) &&
            response != SerialController.SERIAL_DEVICE_CONNECTED &&
            response != SerialController.SERIAL_DEVICE_DISCONNECTED)
        {
            Debug.Log("Respon dari Arduino: " + response);
        }
    }
}
