using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TutorialStep", menuName = "Tutorial/Step", order = 0)]
public class TutorialStep : ScriptableObject
{
    [Header("Teks")]
    public string Title;
    [TextArea] public string Instruction;

    [Header("Target & Goal")]
    public TutorialGoalType GoalType = TutorialGoalType.None;

    [Tooltip("HVRGrabbable untuk Grab/Socket, atau HVRButton untuk Press.")]
    public GameObject Target;

    [Tooltip("GameObject Socket (punya SocketGoalDetectorUniversal).")]
    public GameObject Socket;

    [Tooltip("Dipakai kalau hanya satu zona (opsional).")]
    public Collider Zone;

    [Tooltip("Tambahan zona (opsional).")]
    public List<Collider> AdditionalZones = new();

    // ======== Tambahan: lookup zona dari Scene TANPA script baru ========
    [Header("Zona via Lookup (tanpa reference scene)")]
    [Tooltip("Nama GameObject di scene (harus persis). Isi beberapa nama jika multi-zona).")]
    public List<string> ZoneNames = new();          // contoh: "Zone", "Pipes_01 (7)", "Pipes_01 (11)"

    [Tooltip("Kalau diisi, manager akan cari semua GameObject bertag ini dan ambil Collider-nya.")]
    public string ZoneTag = "";                     // contoh: "TutorialZone"

    [Tooltip("Kalau true, harus masuk SEMUA zona. Kalau false, cukup salah satu.")]
    public bool RequireAllZones = true;
    // ====================================================================

    [Header("Kontrol waktu / lain")]
    [Tooltip("Untuk GoalType = WaitSeconds.")]
    public float WaitDuration = 2f;

    [Header("Visual / UX (opsional)")]
    [Tooltip("Target highlight line. Jika kosong akan auto pilih Target/Socket/Zone.")]
    public Transform HighlightTarget;

    [Header("Opsional")]
    [Tooltip("Jika GoalType = None, auto lanjut setelah delay ini.")]
    public bool AutoContinue = false;

    [Tooltip("Delay sebelum auto-continue ketika AutoContinue = true.")]
    public float AutoContinueDelay = 0.5f;
}
