using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Hurricane VR
using HurricaneVR.Framework.Components;
using HurricaneVR.Framework.Core.Grabbers;
using HurricaneVR.Framework.Core;

public class TutorialManager : MonoBehaviour
{
    [Header("Data")]
    public TutorialSequence Sequence;

    [Header("UI")]
    public TMP_Text TitleText;
    public TMP_Text BodyText;
    public TMP_Text ProgressText;

    [Header("Highlight Line (Opsional)")]
    public LinePointer Line;
    public Transform LineStartPoint;
    public Vector3 LineTargetOffset = new Vector3(0f, 0.2f, 0f);

    [Header("UI Follow Camera")]
    public Transform UIRoot;
    public float UIDistance = 1.2f;
    public Vector3 UIOffset = new Vector3(0f, -0.1f, 0f);
    public bool UIFaceCamera = true;
    public bool SnapUIOnStepComplete = true;
    public bool SnapUIOnStepStart = false;

    // Runtime
    private int _index = -1;
    private Coroutine _waitRoutine;
    private bool _stepCompleting;

    // Zone tracking
    private readonly List<ZoneGoalDetector> _activeZones = new();
    private readonly HashSet<ZoneGoalDetector> _triggeredZones = new();

    // Pegang subscription ke event Grabbed agar gampang di-unsubscribe
    private readonly Dictionary<HVRGrabbable, UnityEngine.Events.UnityAction<HVRGrabberBase, HVRGrabbable>> _grabSubs
        = new();

    private void Start()
    {
        if (Sequence == null || Sequence.Steps == null || Sequence.Steps.Length == 0)
        {
            Debug.LogError("[Tutorial] Sequence kosong.");
            enabled = false;
            return;
        }

        if (Line)
        {
            if (!Line.StartPoint)
            {
                var cam = SafeMainCam();
                Line.StartPoint = LineStartPoint ? LineStartPoint : cam;
            }
            Line.TargetOffset = LineTargetOffset;
            Line.ClearTarget();
        }

        GoNext();
    }

    private void OnDisable()
    {
        // Bersihkan listener saat dimatikan
        StopWait();
        UnwireCurrent();
        UnwireAllGrabSubs();
    }

    private void OnDestroy()
    {
        // Safety net saat dihancurkan
        StopWait();
        UnwireCurrent();
        UnwireAllGrabSubs();
    }

    // ========================= Navigasi =========================
    public void GoNext()
    {
        StopWait();
        UnwireCurrent();

        _index++;
        _stepCompleting = false;

        if (_index >= Sequence.Steps.Length) { FinishTutorial(); return; }

        var step = Sequence.Steps[_index];
        UpdateUI(step);
        SetLineTargetFor(step);

        if (SnapUIOnStepStart) SnapUIToCamera();

        WireForStep(step);
    }

    public void GoPrev()
    {
        StopWait();
        UnwireCurrent();

        _index = Mathf.Max(0, _index - 1);
        _stepCompleting = false;

        var step = Sequence.Steps[_index];
        UpdateUI(step);
        SetLineTargetFor(step);

        if (SnapUIOnStepStart) SnapUIToCamera();

        WireForStep(step);
    }

    private void WireForStep(TutorialStep step)
    {
        switch (step.GoalType)
        {
            case TutorialGoalType.None: AutoOrWait(step); break;
            case TutorialGoalType.WaitSeconds: _waitRoutine = StartCoroutine(CoWait(step.WaitDuration, step)); break;
            case TutorialGoalType.GrabObject: WireGrab(step); break;
            case TutorialGoalType.PressButton: WireButton(step); break;
            case TutorialGoalType.EnterZone:
            case TutorialGoalType.TeleportToZone: WireZone(step); break;
            case TutorialGoalType.SocketObject: WireSocket(step); break;
        }
    }

    // ========================= UI & selesai =========================
    private void UpdateUI(TutorialStep step)
    {
        if (TitleText) TitleText.text = step.Title;
        if (BodyText) BodyText.text = step.Instruction;
        if (ProgressText) ProgressText.text = $"Step {_index + 1}/{Sequence.Steps.Length}";
    }

    private void UpdateZoneProgressUI()
    {
        if (!ProgressText) return;
        var baseText = $"Step {_index + 1}/{Sequence.Steps.Length}";
        if (_activeZones.Count > 0)
            ProgressText.text = $"{baseText}  (Zone {_triggeredZones.Count}/{_activeZones.Count})";
        else
            ProgressText.text = baseText;
    }

    private void FinishTutorial()
    {
        if (TitleText) TitleText.text = "Selesai 🎉";
        if (BodyText) BodyText.text = "Tutorial selesai. Silakan lanjut ke simulasi.";
        if (ProgressText) ProgressText.text = $"{Sequence.Steps.Length}/{Sequence.Steps.Length}";
        if (Line) Line.ClearTarget();

        SnapUIToCamera();
    }

    private void AutoOrWait(TutorialStep step)
    {
        if (step.AutoContinue)
            _waitRoutine = StartCoroutine(CoWait(step.AutoContinueDelay, step));
    }

    private IEnumerator CoWait(float seconds, TutorialStep step)
    {
        yield return new WaitForSeconds(seconds);
        Complete(step);
    }

    private void Complete(TutorialStep _)
    {
        if (_stepCompleting) return;
        _stepCompleting = true;

        if (SnapUIOnStepComplete) SnapUIToCamera();

        GoNext();
    }

    private void StopWait()
    {
        if (_waitRoutine != null) { StopCoroutine(_waitRoutine); _waitRoutine = null; }
    }

    // ========================= UI Follow Camera =========================
    private void SnapUIToCamera()
    {
        if (!UIRoot) return;

        var cam = SafeMainCam();
        if (!cam) return;

        Vector3 targetPos = cam.position + cam.forward * UIDistance + cam.TransformVector(UIOffset);
        UIRoot.position = targetPos;

        if (UIFaceCamera)
        {
            Vector3 fwd = cam.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = cam.forward;
            UIRoot.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }
    }

    private Transform SafeMainCam()
    {
        var c = Camera.main ? Camera.main.transform : null;
        if (!c)
        {
            var cam = FindObjectOfType<Camera>();
            if (cam) c = cam.transform;
        }
        return c;
    }

    // ========================= Highlight Target =========================
    private void SetLineTargetFor(TutorialStep step)
    {
        if (!Line) return;

        Transform hi = step.HighlightTarget
            ? step.HighlightTarget
            : (step.Target ? step.Target.transform
                : (step.Socket ? step.Socket.transform
                    : (step.Zone ? step.Zone.transform : null)));

        if (hi) { Line.TargetOffset = LineTargetOffset; Line.SetTarget(hi); }
        else { Line.ClearTarget(); }
    }

    // ========================= Wiring per Goal =========================
    private void WireGrab(TutorialStep step)
    {
        if (!step.Target) { Debug.LogWarning("[Tutorial] GrabObject tanpa Target."); AutoOrWait(step); return; }

        var list = new List<HVRGrabbable>();
        var gSelf = step.Target.GetComponent<HVRGrabbable>();
        if (gSelf) list.Add(gSelf);
        var gChildren = step.Target.GetComponentsInChildren<HVRGrabbable>(true);
        foreach (var g in gChildren) if (!list.Contains(g)) list.Add(g);

        if (list.Count == 0)
        {
            Debug.LogWarning($"[Tutorial] '{step.Target.name}' tidak punya HVRGrabbable di hierarchy.");
            AutoOrWait(step);
            return;
        }

        bool alreadyHeld = false;

        foreach (var g in list)
        {
            // QoL diag
            var col = g.GetComponent<Collider>() ?? g.GetComponentInChildren<Collider>(true);
            var rb = g.GetComponent<Rigidbody>() ?? g.GetComponentInParent<Rigidbody>();
            if (!col) Debug.LogWarning($"[Tutorial] '{g.name}' tidak punya Collider (non-trigger) → grab bisa gagal.");
            if (!rb) Debug.LogWarning($"[Tutorial] '{g.name}' tidak punya Rigidbody (boleh kinematic).");

            // Bersihkan subs lama (aman jika ada sisa dari langkah sebelumnya)
            if (_grabSubs.TryGetValue(g, out var old))
            {
                g.Grabbed.RemoveListener(old);
                _grabSubs.Remove(g);
            }

            // Subscribe baru
            UnityEngine.Events.UnityAction<HVRGrabberBase, HVRGrabbable> handler =
                (grabber, grabbed) =>
                {
                    Debug.Log($"[Tutorial] Grabbed → {grabbed.name} oleh {grabber.name} → lanjut step");
                    OnStepDone();
                };

            g.Grabbed.AddListener(handler);
            _grabSubs[g] = handler;

            // Cek “sudah dipegang” (lintas versi)
            try
            {
                var hands = g.HandGrabbers; // 2.9.x
                if (hands != null && hands.Count > 0) alreadyHeld = true;
            }
            catch { /* ignore */ }

            try
            {
                var isHeldProp = g.GetType().GetProperty("IsBeingHeld") ?? g.GetType().GetProperty("IsGrabbed");
                if (isHeldProp != null)
                {
                    var val = isHeldProp.GetValue(g, null);
                    if (val is bool b && b) alreadyHeld = true;
                }
            }
            catch { /* ignore */ }

            if (alreadyHeld) break;
        }

        if (alreadyHeld)
        {
            Debug.Log("[Tutorial] Salah satu grabbable sudah dipegang saat step mulai → auto complete.");
            OnStepDone();
            UpdateZoneProgressUI();
        }
    }

    private void WireButton(TutorialStep step)
    {
        if (!step.Target) { Debug.LogWarning("[Tutorial] PressButton tanpa Target."); AutoOrWait(step); return; }

        var btn = step.Target.GetComponent<HVRButton>();
        if (!btn) { Debug.LogWarning("[Tutorial] Target tidak memiliki HVRButton."); AutoOrWait(step); return; }

        // Pastikan tidak double-subscribe jika kembali ke step ini
        var existing = step.Target.GetComponent<ButtonGoalDetector>();
        if (!existing) existing = step.Target.AddComponent<ButtonGoalDetector>();

        existing.Button = btn;
        existing.OnPressed -= OnStepDone;
        existing.OnPressed += OnStepDone;
        existing.enabled = true;
    }

    private void WireZone(TutorialStep step)
    {
        _activeZones.Clear();
        _triggeredZones.Clear();

        var colliders = ResolveZonesFromStep(step);

        if (colliders.Count == 0)
        {
            Debug.LogWarning("[Tutorial] EnterZone: tidak ada collider yang ditemukan (cek Zone / AdditionalZones / ZoneNames / ZoneTag).");
            AutoOrWait(step);
            return;
        }

        foreach (var col in colliders)
        {
            if (!col) continue;

            var det = col.GetComponent<ZoneGoalDetector>() ?? col.gameObject.AddComponent<ZoneGoalDetector>();
            det.OnEntered -= OnZoneEntered;
            det.OnEntered += OnZoneEntered;
            det.enabled = true;

            if (!col.isTrigger) col.isTrigger = true;

            _activeZones.Add(det);
        }

        //UpdateZoneProgressUI();
    }

    private void OnZoneEntered(ZoneGoalDetector zone)
    {
        var step = Sequence.Steps[_index];
        bool requireAll = false;
        try { requireAll = step.RequireAllZones; } catch { }

        if (!requireAll)
        {
            foreach (var z in _activeZones) if (z) z.OnEntered -= OnZoneEntered;
            _activeZones.Clear();
            _triggeredZones.Clear();
            OnStepDone();
            return;
        }

        if (_triggeredZones.Add(zone))
        {
            Debug.Log($"[Tutorial] Zone triggered {_triggeredZones.Count}/{_activeZones.Count} : {zone.gameObject.name}");
           // UpdateZoneProgressUI();
        }

        if (_triggeredZones.Count == _activeZones.Count)
        {
            foreach (var z in _activeZones) if (z) z.OnEntered -= OnZoneEntered;
            _activeZones.Clear();
            _triggeredZones.Clear();
            OnStepDone();
        }
    }

    private void WireSocket(TutorialStep step)
    {
        if (!step.Target || !step.Socket) { Debug.LogWarning("[Tutorial] SocketObject butuh Target & Socket."); AutoOrWait(step); return; }

        var grabbable = step.Target.GetComponent<HVRGrabbable>()
                       ?? step.Target.GetComponentInChildren<HVRGrabbable>(true);
        if (!grabbable) { Debug.LogWarning("[Tutorial] Target bukan HVRGrabbable."); AutoOrWait(step); return; }

        var detUni = step.Socket.GetComponent<SocketGoalDetectorUniversal>() ?? step.Socket.AddComponent<SocketGoalDetectorUniversal>();
        detUni.Target = grabbable;
        detUni.SocketRoot = step.Socket.transform;

        var anchor = step.Socket.transform.Find("SocketAnchor");
        detUni.SocketAnchor = anchor ? anchor : step.Socket.transform;

        detUni.OnSocketed -= OnStepDone;
        detUni.OnSocketed += OnStepDone;
        detUni.enabled = true;
    }

    private void OnStepDone()
    {
        Debug.Log($"[Tutorial] OnStepDone at step {_index}");
        Complete(Sequence.Steps[_index]);
    }

    // ========================= Helpers =========================
    private List<Collider> ResolveZonesFromStep(TutorialStep step)
    {
        var zones = new List<Collider>();
        var seen = new HashSet<Collider>();

        void Add(Collider c)
        {
            if (!c || seen.Contains(c)) return;
            seen.Add(c);
            zones.Add(c);
        }

        if (step.Zone) Add(step.Zone);
        if (step.AdditionalZones != null)
            foreach (var c in step.AdditionalZones) Add(c);

        if (step.ZoneNames != null && step.ZoneNames.Count > 0)
        {
            foreach (var name in step.ZoneNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var go = GameObject.Find(name);
                if (!go) { Debug.LogWarning($"[Tutorial] ZoneName '{name}' tidak ditemukan di scene."); continue; }

                var col = go.GetComponent<Collider>();
                if (!col) { Debug.LogWarning($"[Tutorial] '{name}' tidak punya Collider."); continue; }
                Add(col);
            }
        }

        if (!string.IsNullOrWhiteSpace(step.ZoneTag))
        {
            var tagged = GameObject.FindGameObjectsWithTag(step.ZoneTag);
            foreach (var go in tagged)
            {
                var col = go.GetComponent<Collider>();
                if (col) Add(col);
            }
        }

        return zones;
    }

    // ========================= Unwire =========================
    private void UnwireCurrent()
    {
        if (_index < 0 || _index >= Sequence.Steps.Length) return;

        var step = Sequence.Steps[_index];

        if (step.Target)
        {
            // Lepas listener Grabbed untuk SEMUA HVRGrabbable pada target hierarchy
            var list = new List<HVRGrabbable>();
            var gSelf = step.Target.GetComponent<HVRGrabbable>();
            if (gSelf) list.Add(gSelf);
            var gChildren = step.Target.GetComponentsInChildren<HVRGrabbable>(true);
            foreach (var g in gChildren) if (!list.Contains(g)) list.Add(g);

            foreach (var g in list)
            {
                if (g != null && _grabSubs.TryGetValue(g, out var sub))
                {
                    g.Grabbed.RemoveListener(sub);
                    _grabSubs.Remove(g);
                }
            }

            // Button detector
            var btnDet = step.Target.GetComponent<ButtonGoalDetector>();
            if (btnDet) btnDet.OnPressed -= OnStepDone;

            // GrabGoalDetector (kalau kebetulan dipakai)
            var grabDet = step.Target.GetComponent<GrabGoalDetector>();
            if (grabDet) grabDet.OnGrab -= OnStepDone;
        }

        // Zone listeners
        foreach (var z in _activeZones) if (z) z.OnEntered -= OnZoneEntered;
        _activeZones.Clear();
        _triggeredZones.Clear();

        // Socket listener
        if (step.Socket)
        {
            var sockDet = step.Socket.GetComponent<SocketGoalDetectorUniversal>();
            if (sockDet) sockDet.OnSocketed -= OnStepDone;
        }
    }

    private void UnwireAllGrabSubs()
    {
        // Bersihkan semua sisa subs kalau ada (guard tambahan)
        if (_grabSubs.Count == 0) return;

        // Kumpulkan dulu supaya aman dari "collection modified"
        var pairs = new List<KeyValuePair<HVRGrabbable, UnityEngine.Events.UnityAction<HVRGrabberBase, HVRGrabbable>>>(_grabSubs);

        foreach (var kv in pairs)
        {
            var g = kv.Key;
            var sub = kv.Value;
            if (g) g.Grabbed.RemoveListener(sub);
            _grabSubs.Remove(g);
        }
    }
}
