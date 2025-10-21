using Microsoft.MixedReality.Toolkit.Experimental.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ShowKeyboard : MonoBehaviour
{
    TMP_InputField inputfield;

    public float distance = 0.5f;
    public float verticalOffset = -0.5f;

    public Transform positionSource;
    void Start()
    {
        inputfield = GetComponent<TMP_InputField>();
        inputfield.onSelect.AddListener(x => OpenKeyboard());
    }


    public void OpenKeyboard()
    {
        NonNativeKeyboard.Instance.InputField = inputfield;
        NonNativeKeyboard.Instance.PresentKeyboard(inputfield.text);
        Vector3 direction = positionSource.forward;
        direction.y = 0;
        direction.Normalize();

        Vector3 targetPosition = positionSource.position + direction * distance + Vector3.up * verticalOffset;

        NonNativeKeyboard.Instance.RepositionKeyboard(targetPosition);
        SetCaretColorAlpha(1);
        NonNativeKeyboard.Instance.OnClosed += Instance_OnClosed;
    }

    private void Instance_OnClosed(object sender, System.EventArgs e)
    {
        SetCaretColorAlpha(0);
        NonNativeKeyboard.Instance.OnClosed -= Instance_OnClosed;

    }
    // Update is called once per frame
    public void SetCaretColorAlpha(float value)
    {
        inputfield.customCaretColor = true;
        Color caretColor = inputfield.caretColor;
        caretColor.a = value;
        inputfield.caretColor = caretColor;
    }
}
