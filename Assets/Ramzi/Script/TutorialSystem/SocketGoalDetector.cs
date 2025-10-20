using HurricaneVR.Framework.Core.Grabbers;
using HurricaneVR.Framework.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using HurricaneVR.Framework.Core.Sockets;

public class SocketGoalDetector : MonoBehaviour
{
    public HVRGrabbable Grabbable;   // drag di Inspector
    public event Action OnGrab;

    private bool wired;

    void Start()
    {
        if (!Grabbable) { Debug.LogError("GrabGoalDetector: Grabbable kosong."); enabled = false; return; }

        // Event resmi & stabil di HVR
        if (!wired)
        {
            Grabbable.Grabbed.AddListener(OnGrabbed);
            wired = true;
        }
    }

    private void OnGrabbed(HVRGrabberBase grabber, HVRGrabbable grabbable)
    {
        OnGrab?.Invoke();
        // jika hanya sekali
        Grabbable.Grabbed.RemoveListener(OnGrabbed);
        enabled = false;
    }
}
