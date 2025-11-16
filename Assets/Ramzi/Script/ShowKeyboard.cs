using Microsoft.MixedReality.Toolkit.Experimental.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TMP_InputField))]
public class ShowKeyboard : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Transform positionSource;

    [Header("Keyboard Placement")]
    [SerializeField] private float distance = 0.5f;
    [SerializeField] private float verticalOffset = -0.5f;

    [Header("Caret Settings (Debug)")]
    [SerializeField] private Color caretVisibleColor = Color.cyan; // warna mencolok
    [SerializeField] private float caretBlinkRate = 0.7f;          // > 0 supaya kedip
    [SerializeField] private int caretWidth = 4;                   // tebal biar kelihatan

    private NonNativeKeyboard keyboard;

    // ==== Owner logic (biar nggak lag kalau ada 2 ShowKeyboard) ====
    private static ShowKeyboard activeOwner = null;
    private bool isActiveOwner = false;

    private void Awake()
    {
        if (!inputField)
            inputField = GetComponent<TMP_InputField>();

        keyboard = NonNativeKeyboard.Instance;

        // Penting di Android / Quest:
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

        // Lepas owner kalau script ini kebetulan yang aktif
        if (activeOwner == this)
            activeOwner = null;

        isActiveOwner = false;
    }

    private void Update()
    {
        if (!inputField) return;
        if (keyboard == null) return;

        // Hanya pemilik aktif yang boleh ngurus keyboard & fokus
        if (!isActiveOwner || activeOwner != this)
            return;

        // Selama keyboard masih aktif, jagain supaya inputField tetap fokus di VR
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

        // Kalau sedang fokus, pastikan caret-nya nggak diubah script lain
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
        // Kalau keyboard sudah aktif dan owner-nya kita,
        // jangan buka + reposition lagi (biar keyboard nggak "lari-lari" saat ngetik).
        if (keyboard != null &&
            keyboard.gameObject.activeInHierarchy &&
            activeOwner == this)
        {
            EnsureFocusOnly();
            return;
        }

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

        // 🔒 Kalau keyboard sudah aktif untuk field ini,
        // jangan reposition/present lagi supaya tidak "lari-lari" saat ngetik.
        if (keyboard.gameObject.activeInHierarchy && activeOwner == this)
        {
            EnsureFocusOnly();
            return;
        }

        // Fallback posisi
        if (!positionSource)
        {
            if (Camera.main != null)
                positionSource = Camera.main.transform;
            else
                positionSource = transform;
        }

        // Kalau owner lama beda, lepas fokus dia dulu
        if (activeOwner != null && activeOwner != this)
        {
            activeOwner.ReleaseFocus();
        }

        // Set owner baru (input field yang barusan di-select)
        activeOwner = this;
        isActiveOwner = true;

        // Bind input field ke keyboard
        keyboard.InputField = inputField;
        keyboard.PresentKeyboard(inputField != null ? inputField.text : "");

        // Hitung posisi keyboard di depan user
        Vector3 direction = positionSource.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = positionSource.forward;
        direction.Normalize();

        Vector3 targetPosition =
            positionSource.position +
            direction * distance +
            Vector3.up * verticalOffset;

        keyboard.RepositionKeyboard(targetPosition);

        // === Fokuskan input field & paksa caret hidup ===
        EnsureFocusOnly();

        UnsubscribeKeyboardEvents();
        keyboard.OnClosed += Instance_OnClosed;

        // Trik refresh TMP supaya caret benar-benar muncul di VR
        StartCoroutine(RefreshInputFieldNextFrame());
    }

    private System.Collections.IEnumerator RefreshInputFieldNextFrame()
    {
        // Tunggu 1 frame dulu
        yield return null;

        if (!inputField) yield break;

        // Re-enable memaksa TMP rebuild mesh (termasuk caret)
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

        // Di tahap debug ini caret tidak disembunyikan,
        // supaya kelihatan apakah dia benar-benar muncul.
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

        // pastikan alpha = 1
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
