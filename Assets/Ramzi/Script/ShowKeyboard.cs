using Microsoft.MixedReality.Toolkit.Experimental.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TMP_InputField))]
public class ShowKeyboard : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Transform positionSource; // biasanya head/camera

    [Header("Keyboard Placement")]
    [SerializeField] private float distance = 0.5f;
    [SerializeField] private float verticalOffset = -0.5f;

    [Header("Caret Settings (Debug)")]
    [SerializeField] private Color caretVisibleColor = Color.cyan; // warna mencolok
    [SerializeField] private float caretBlinkRate = 0.7f;          // > 0 supaya kedip
    [SerializeField] private int caretWidth = 4;                   // tebal biar kelihatan

    private NonNativeKeyboard keyboard;

    // ==== Owner logic (biar nggak tabrakan kalau ada banyak ShowKeyboard) ====
    private static ShowKeyboard activeOwner = null;
    private bool isActiveOwner = false;

    private void Awake()
    {
        if (!inputField)
            inputField = GetComponent<TMP_InputField>();

        keyboard = NonNativeKeyboard.Instance;

        // Penting untuk Android / Quest:
        inputField.shouldHideMobileInput = true;   // sembunyikan input box native
#if UNITY_ANDROID
        inputField.shouldHideSoftKeyboard = true;  // paksa TMP yang urus caret & input
#endif

        ForceCaretOn();
    }

    private void OnEnable()
    {
        if (inputField != null)
            inputField.onSelect.AddListener(OnInputSelected);
    }

    private void OnDisable()
    {
        if (inputField != null)
            inputField.onSelect.RemoveListener(OnInputSelected);

        UnsubscribeKeyboardEvents();

        if (activeOwner == this)
            activeOwner = null;

        isActiveOwner = false;
    }

    private void Update()
    {
        if (!inputField || keyboard == null) return;
        if (!isActiveOwner || activeOwner != this) return;

        // Jaga supaya inputField tetap fokus ketika keyboard aktif
        if (keyboard.gameObject.activeInHierarchy)
        {
            if (!inputField.isFocused)
            {
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(inputField.gameObject);

                inputField.ActivateInputField();
                inputField.caretPosition = inputField.text.Length;
            }
        }

        // Kalau fokus, pastikan caret tetap kelihatan
        if (inputField.isFocused)
        {
            if (!inputField.customCaretColor ||
                inputField.caretColor.a < 0.95f ||
                inputField.caretWidth < caretWidth)
            {
                ForceCaretOn();
            }
        }
    }

    private void OnInputSelected(string _)
    {
        // Pastikan instance keyboard ada
        if (keyboard == null)
        {
            keyboard = NonNativeKeyboard.Instance;
            if (keyboard == null)
            {
                Debug.LogWarning("NonNativeKeyboard.Instance tidak ditemukan di scene.");
                return;
            }
        }

        // Fallback posisi sumber (kamera)
        if (!positionSource)
        {
            if (Camera.main != null)
                positionSource = Camera.main.transform;
            else
                positionSource = transform;
        }

        // === Jika keyboard SUDAH aktif ===
        if (keyboard.gameObject.activeInHierarchy)
        {
            // Kalau owner lama beda, lepas dulu
            if (activeOwner != null && activeOwner != this)
            {
                activeOwner.ReleaseFocus();
            }

            // Set owner ke input field ini
            activeOwner = this;
            isActiveOwner = true;

            // Ganti binding input field (kalau ganti field)
            keyboard.InputField = inputField;

            // ⚡ Snap posisi & rotasi ke kamera SETIAP TAP
            PlaceKeyboardOnce();

            EnsureFocusOnly();
            return;
        }

        // === Kalau keyboard belum aktif, buka baru ===
        OpenKeyboard();
    }

    public void OpenKeyboard()
    {
        if (keyboard == null)
        {
            keyboard = NonNativeKeyboard.Instance;
            if (keyboard == null)
            {
                Debug.LogWarning("NonNativeKeyboard.Instance tidak ditemukan di scene.");
                return;
            }
        }

        if (!positionSource)
        {
            if (Camera.main != null)
                positionSource = Camera.main.transform;
            else
                positionSource = transform;
        }

        // Kalau owner lama beda, lepas dulu
        if (activeOwner != null && activeOwner != this)
        {
            activeOwner.ReleaseFocus();
        }

        // Set owner baru
        activeOwner = this;
        isActiveOwner = true;

        // Bind input field ke keyboard
        keyboard.InputField = inputField;
        keyboard.PresentKeyboard(inputField != null ? inputField.text : "");

        // Posisi & rotasi sekali saat dibuka
        PlaceKeyboardOnce();

        EnsureFocusOnly();

        UnsubscribeKeyboardEvents();
        keyboard.OnClosed += Instance_OnClosed;

        StartCoroutine(RefreshInputFieldNextFrame());
    }

    private void PlaceKeyboardOnce()
    {
        if (!positionSource || keyboard == null) return;

        // Arah depan kamera (yaw saja, y = 0 biar nggak terlalu naik-turun)
        Vector3 dir = positionSource.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = positionSource.forward;
        dir.Normalize();

        // Posisi keyboard di depan kamera
        Vector3 targetPosition =
            positionSource.position +
            dir * distance +
            Vector3.up * verticalOffset;

        // Rotasi keyboard menghadap arah pandang kamera (di bidang horizontal)
        Quaternion targetRotation = Quaternion.LookRotation(dir, Vector3.up);

        keyboard.RepositionKeyboard(targetPosition);
        keyboard.transform.rotation = targetRotation;
    }

    private System.Collections.IEnumerator RefreshInputFieldNextFrame()
    {
        yield return null;

        if (!inputField) yield break;

        inputField.enabled = false;
        inputField.enabled = true;

        EnsureFocusOnly();
    }

    private void Instance_OnClosed(object sender, System.EventArgs e)
    {
        UnsubscribeKeyboardEvents();

        if (activeOwner == this)
            activeOwner = null;

        isActiveOwner = false;
    }

    private void UnsubscribeKeyboardEvents()
    {
        if (keyboard != null)
            keyboard.OnClosed -= Instance_OnClosed;
    }

    private void ForceCaretOn()
    {
        if (!inputField) return;

        inputField.customCaretColor = true;

        caretVisibleColor.a = 1f;
        inputField.caretColor = caretVisibleColor;

        inputField.caretBlinkRate = (caretBlinkRate <= 0f) ? 0.7f : caretBlinkRate;
        inputField.caretWidth = Mathf.Max(4, caretWidth);
    }

    private void EnsureFocusOnly()
    {
        if (!inputField) return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(inputField.gameObject);

        inputField.readOnly = false;
        inputField.caretPosition = inputField.text.Length;
        inputField.selectionAnchorPosition = inputField.caretPosition;
        inputField.selectionFocusPosition = inputField.caretPosition;

        ForceCaretOn();
        inputField.ActivateInputField();
        inputField.ForceLabelUpdate();
    }

    private void ReleaseFocus()
    {
        if (!inputField) return;
        inputField.DeactivateInputField();
    }
}
