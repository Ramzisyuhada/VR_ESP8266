using UnityEngine;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Grabbers;
using System;

[RequireComponent(typeof(HVRGrabbable))]
[RequireComponent(typeof(Rigidbody))]
public class SnapOnReleaseByDistance : MonoBehaviour
{
    [Header("Target Snap")]
    public Transform snapTarget;               // posisi snap tujuan (drag di inspector)
    public float snapDistance = 0.25f;         // jarak maksimum bisa snap
    public bool parentToTarget = true;         // jadi anak target setelah snap

    [Header("Visual Ghost")]
    public GameObject previewPrefab;           // prefab visual ghost
    public Vector3 positionOffset;             // offset dari target
    public Vector3 rotationOffsetEuler;        // offset rotasi
    public float previewFade = 0.4f;

    private HVRGrabbable grabbable;
    private Rigidbody rb;
    private GameObject preview;

    public event Action<Transform> OnSnapped;   // event saat tersnap
    public event Action OnUnsnapped;            // event saat dilepas dari snap
    private bool isSnapped = false;
    public bool IsSnapped => isSnapped;

    [Header("MeshCollider Convex Control")]
    public bool autoToggleConvex = true;        // aktifkan untuk kontrol otomatis
    private MeshCollider[] meshCols;            // cache semua MeshCollider

    public bool keepPreviewWhenSnapped = true;

    private void Awake()
    {
        meshCols = GetComponentsInChildren<MeshCollider>(true);
        grabbable = GetComponent<HVRGrabbable>();
        rb = GetComponent<Rigidbody>();

        grabbable.Released.AddListener(OnReleased);
        grabbable.Grabbed.AddListener(OnGrabbed);

        CreatePreview();
        SetMeshConvex(true); // pastikan convex diawal (bisa digrab)
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

    private void Update()
    {
        if (!snapTarget || !preview) return;

        // Posisi & rotasi preview (di target + offset)
        Vector3 p = snapTarget.TransformPoint(positionOffset);
        Quaternion r = snapTarget.rotation * Quaternion.Euler(rotationOffsetEuler);

        if (isSnapped)
        {
            if (keepPreviewWhenSnapped)
            {
                preview.SetActive(true);
                preview.transform.SetPositionAndRotation(p, r);
            }
            else
            {
                preview.SetActive(false);
            }
            return;
        }

        // Belum snapped
        float dist = Vector3.Distance(transform.position, snapTarget.position);
        bool isHeld = grabbable.HandGrabbers != null && grabbable.HandGrabbers.Count > 0;
        bool show = isHeld && dist <= snapDistance;

        preview.SetActive(show);
        if (show)
            preview.transform.SetPositionAndRotation(p, r);
    }

    // Saat dipegang
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

        SetMeshConvex(true); // pastikan collider bisa digrab
        Debug.Log("[Snap] OnGrabbed -> collider convex aktif");
    }

    // Saat dilepas
    private void OnReleased(HVRGrabberBase grabber, HVRGrabbable g)
    {
        if (!snapTarget) return;

        float dist = Vector3.Distance(transform.position, snapTarget.position);
        if (dist <= snapDistance)
        {
            SnapToTarget();
        }
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

        // tetap convex biar bisa digrab ulang
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
                mc.isTrigger = false; // non-convex tidak boleh trigger
        }
    }

    private void CreatePreview()
    {
        if (!previewPrefab || !snapTarget) return;

        preview = Instantiate(previewPrefab);
        preview.name = "[SnapPreview]";
        preview.SetActive(false);

        var mr = preview.GetComponentInChildren<Renderer>();
        if (!mr) return;

        Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");

        var mat = new Material(urpUnlit ? urpUnlit : urpLit);

        Color c = new Color(0f, 0.6f, 1f, previewFade);
        if (urpUnlit)
        {
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Surface", 1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Surface", 1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        mr.sharedMaterial = mat;
    }
}
