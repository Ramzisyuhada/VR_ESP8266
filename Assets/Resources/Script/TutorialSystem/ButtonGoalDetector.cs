using System;
using UnityEngine;
// pastikan pakai namespace HVR:
using HurricaneVR.Framework.Components;

public class ButtonGoalDetector : MonoBehaviour
{
    [Header("HVR Button Target")]
    public HVRButton Button;   // drag HVRButton di Inspector
    public bool Once = true;

    public event Action OnPressed;

    private bool _fired;

    void Awake()
    {
        if (!Button) Button = GetComponent<HVRButton>();
    }

    void OnEnable()
    {
        if (!Button)
        {
            Debug.LogError("ButtonGoalDetector: HVRButton belum di-assign!");
            enabled = false;
            return;
        }

        // ✅ cocok dengan UnityEvent<HVRButton>
        Button.ButtonDown.AddListener(OnButtonDownWithArg);

        // Kalau di versimu kebetulan event-nya tanpa argumen,
        // tinggal ganti baris di atas jadi:
        // Button.ButtonDown.AddListener(OnButtonDownNoArg);
    }

    void OnDisable()
    {
        if (Button)
        {
            Button.ButtonDown.RemoveListener(OnButtonDownWithArg);
            // Button.ButtonDown.RemoveListener(OnButtonDownNoArg);
        }
    }

    // Cocok untuk UnityEvent<HVRButton>
    private void OnButtonDownWithArg(HVRButton _)
    {
        Fire();
    }

    private void OnButtonDownNoArg()
    {
        Fire();
    }

    private void Fire()
    {
        if (_fired && Once) return;
        _fired = true;
        OnPressed?.Invoke();
        if (Once) enabled = false;
    }
}
