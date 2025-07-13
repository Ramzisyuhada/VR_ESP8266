using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Switch : MonoBehaviour
{

    [SerializeField]
    private UnityEvent _OnSwitch;
    [SerializeField]
    private UnityEvent _OffSwitch;

    private Vector3 InitPositionSwitch;
    private bool OnSwitch;
    private void OnTriggerEnter(Collider other)
    {
        OnSwitch = !OnSwitch;

    }

    void Start()
    {
        InitPositionSwitch = transform.localPosition;   
    }

    // Update is called once per frame
    void Update()
    {
        if (OnSwitch)
        {
            _OnSwitch?.Invoke();
            transform.localPosition = new Vector3(0.081f, InitPositionSwitch.y, InitPositionSwitch.z);
        }
        else
        {
            _OffSwitch?.Invoke();
            transform.localPosition = InitPositionSwitch;
        }
    }
}
