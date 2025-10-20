using UnityEngine;

/// <summary>
/// Menggambar garis dari StartPoint ke Target (dengan offset) memakai LineRenderer.
/// Bisa diberi efek pulse lebar garis agar lebih menarik.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class LinePointer : MonoBehaviour
{
    public Transform StartPoint;          // ujung awal garis (mis. di depan kamera / samping HUD)
    public Vector3 TargetOffset = new Vector3(0f, 0.2f, 0f);
    public bool PulseWidth = true;
    public float PulseSpeed = 2.0f;
    public float MinWidth = 0.01f;
    public float MaxWidth = 0.03f;

    private LineRenderer _lr;
    private Transform _target;

    void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _lr.positionCount = 2;
        _lr.enabled = false; // default off
        if (_lr.material == null)
        {
            // pakai material default bila belum ada
            _lr.material = new Material(Shader.Find("Sprites/Default"));
        }
    }

    public void SetTarget(Transform t)
    {
        _target = t;
        _lr.enabled = _target != null && StartPoint != null;
        if (_lr.enabled) UpdateLineImmediate();
    }

    public void ClearTarget()
    {
        _target = null;
        _lr.enabled = false;
    }

    void LateUpdate()
    {
        if (!_lr.enabled || _target == null || StartPoint == null) return;

        // posisi garis
        _lr.SetPosition(0, StartPoint.position);
        _lr.SetPosition(1, _target.position + TargetOffset);

        // efek pulse lebar garis (opsional)
        if (PulseWidth)
        {
            float a = (Mathf.Sin(Time.time * Mathf.PI * PulseSpeed) + 1f) * 0.5f; // 0..1
            float w = Mathf.Lerp(MinWidth, MaxWidth, a);
            _lr.startWidth = w;
            _lr.endWidth = w;
        }
    }

    public void UpdateLineImmediate()
    {
        if (_target == null || StartPoint == null) return;
        _lr.SetPosition(0, StartPoint.position);
        _lr.SetPosition(1, _target.position + TargetOffset);
    }
}
