using UnityEngine;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Grabbers;
using System;

[RequireComponent(typeof(HVRGrabbable))]
[RequireComponent(typeof(Rigidbody))]
public class SnapOnReleaseByDistance : MonoBehaviour
{
    [Header("Target Snap")]
    public Transform snapTarget;
    public float snapDistance = 0.25f;
    public bool parentToTarget = true;

    [Header("Visual Ghost")]
    public GameObject previewPrefab;
    public Vector3 positionOffset;
    public Vector3 rotationOffsetEuler;
    public float previewFade = 0.4f;

    // 🔵🟢 Preview behavior & colors
    [Header("Preview Behavior & Colors")]
    public bool previewAlwaysOn = true;                              // <<== selalu tampil
    public bool greenWhenWithinDistance = true;                      // <<== hijau saat siap snap
    public Color previewColor = new Color(0f, 0.6f, 1f, 0.35f);      // biru transparan
    public Color readyColor = new Color(0.2f, 1f, 0.2f, 0.5f);     // hijau saat siap
    public bool keepPreviewWhenSnapped = true;

    private HVRGrabbable grabbable;
    private Rigidbody rb;
    private GameObject preview;

    // cache renderer/material preview
    private Renderer previewRenderer;
    private Material previewMat;
    private bool useURPUnlit = false;

    public event Action<Transform> OnSnapped;
    public event Action OnUnsnapped;
    private bool isSnapped = false;
    public bool IsSnapped
    {
        get => isSnapped;
        set => isSnapped = value;
    }

    [Header("MeshCollider Convex Control")]
    public bool autoToggleConvex = true;
    private MeshCollider[] meshCols;


    [Header("Preview Rules")]
    public bool requireHeldForPreview = true;     // preview hanya muncul saat dipegang
    public bool greenOnlyWhenHeld = true;      // hijau hanya kalau dipegang & ready

    private void Awake()
    {
        meshCols = GetComponentsInChildren<MeshCollider>(true);
        grabbable = GetComponent<HVRGrabbable>();
        rb = GetComponent<Rigidbody>();

        grabbable.Released.AddListener(OnReleased);
        grabbable.Grabbed.AddListener(OnGrabbed);

        CreatePreview();
        SetMeshConvex(true); // biar gampang digrab
    }

    private void OnDestroy()
    {
        if (grabbable)
        {
            grabbable.Released.RemoveListener(OnReleased);
            grabbable.Grabbed.RemoveListener(OnGrabbed);
        }
        if (preview) Destroy(preview);
        SetMeshConvex(true);
    }

    // Ganti isi Update()
    private void Update()
    {
        if (!snapTarget || !preview) return;

        Vector3 p = snapTarget.TransformPoint(positionOffset);
        Quaternion r = snapTarget.rotation * Quaternion.Euler(rotationOffsetEuler);

        float dist = Vector3.Distance(transform.position, snapTarget.position);
        bool ready = dist <= snapDistance;
        bool isHeld = grabbable.HandGrabbers != null && grabbable.HandGrabbers.Count > 0;

        // Preview hanya saat dipegang (sesuai syarat kamu)
        bool show = requireHeldForPreview ? isHeld : true;

        // Jika sudah snapped, pakai aturan keepPreviewWhenSnapped
        if (isSnapped) show = keepPreviewWhenSnapped;

        preview.SetActive(show);
        if (!show) return;

        preview.transform.SetPositionAndRotation(p, r);

        // Warna: hijau hanya jika dipegang & ready
        if (greenOnlyWhenHeld)
            SetPreviewColor((isHeld && ready) ? readyColor : previewColor);
        else
            SetPreviewColor(ready ? readyColor : previewColor);
    }

    private void OnGrabbed(HVRGrabberBase grabber, HVRGrabbable g)
    {
        if (isSnapped)
        {
            transform.SetParent(null, true);
            rb.isKinematic = false;
            rb.useGravity = true;
            isSnapped = false;
            OnUnsnapped?.Invoke();
        }

        SetMeshConvex(true);
        Debug.Log("[Snap] OnGrabbed -> collider convex aktif");
    }

    private void OnReleased(HVRGrabberBase grabber, HVRGrabbable g)
    {
        if (!snapTarget) return;

        float dist = Vector3.Distance(transform.position, snapTarget.position);
        if (dist <= snapDistance)
            SnapToTarget();
    }

    private void SnapToTarget()
    {
        Vector3 pos = snapTarget.TransformPoint(positionOffset);
        Quaternion rot = snapTarget.rotation * Quaternion.Euler(rotationOffsetEuler);

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;

        transform.SetPositionAndRotation(pos, rot);
        if (parentToTarget) transform.SetParent(snapTarget);

        // ⚠️ kalau ingin tetap gampang digrab lagi, biarkan TRUE.
        // SetMeshConvex(false);  // <- kalau kamu butuh non-convex pas terpasang, aktifkan tapi pastikan ada "grab handle" convex terpisah.
        SetMeshConvex(false);

        isSnapped = true;
        OnSnapped?.Invoke(snapTarget);
        Debug.Log("[Snap] Berhasil snap ke target!");
    }

    private void SetMeshConvex(bool convex)
    {
        if (!autoToggleConvex || meshCols == null) return;
        foreach (var mc in meshCols)
        {
            if (!mc) continue;
            mc.convex = convex;
            if (!convex && mc.isTrigger)
                mc.isTrigger = false;
        }
    }

    // ---------- Preview setup & color helpers ----------
    private void CreatePreview()
    {
        if (!previewPrefab || !snapTarget) return;

        preview = Instantiate(previewPrefab);
        preview.name = "[SnapPreview]";
        preview.SetActive(false);

        previewRenderer = preview.GetComponentInChildren<Renderer>();
        if (!previewRenderer) return;

        Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        useURPUnlit = urpUnlit != null;

        previewMat = new Material(useURPUnlit ? urpUnlit : urpLit);

        // set awal warna biru transparan
        SetPreviewColor(previewColor);

        // transparansi URP
        previewMat.SetFloat("_Surface", 1f); // Transparent
        previewMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        previewMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        previewRenderer.sharedMaterial = previewMat;
    }

    private void SetPreviewColor(Color color)
    {
        if (previewRenderer == null) return;

        // Ambil semua material yang dipakai renderer
        var mats = previewRenderer.materials;
        foreach (var mat in mats)
        {
            if (!mat) continue;

            // URP pakai _BaseColor, Standard pakai _Color
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            // pastikan transparansi aktif
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

}
