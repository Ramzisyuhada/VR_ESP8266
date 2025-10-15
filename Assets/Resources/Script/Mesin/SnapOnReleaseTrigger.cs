using UnityEngine;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Grabbers;
using System;

[RequireComponent(typeof(HVRGrabbable))]
[RequireComponent(typeof(Rigidbody))]
public class SnapOnReleaseByDistance : MonoBehaviour
{
    [Header("Target Snap")]
    public Transform snapTarget;           // posisi snap tujuan (drag di inspector)
    public float snapDistance = 0.25f;     // jarak maksimum bisa snap
    public bool parentToTarget = true;     // jadi anak target setelah snap

    [Header("Visual Ghost")]
    public GameObject previewPrefab;       // prefab visual ghost
    public Vector3 positionOffset;         // offset dari target
    public Vector3 rotationOffsetEuler;    // offset rotasi
    public float previewFade = 0.4f;

    private HVRGrabbable grabbable;
    private Rigidbody rb;
    private GameObject preview;

    public event Action<Transform> OnSnapped;   
    public event Action OnUnsnapped;
    private bool isSnapped = false;
    public bool IsSnapped => isSnapped;

    public bool keepPreviewWhenSnapped = true;


    // === MeshCollider toggle ===
    [Header("MeshCollider Convex Toggle")]
    public bool toggleMeshConvex = true;   // centang kalau mau otomatis ubah convex
    private MeshCollider[] meshCols;       // cache semua MeshCollider di objek (termasuk anak)

    private void Awake()
    {
        if(GetComponentsInChildren<MeshCollider>() != null) meshCols = GetComponentsInChildren<MeshCollider>(true);

        grabbable = GetComponent<HVRGrabbable>();
        rb = GetComponent<Rigidbody>();

        grabbable.Released.AddListener(OnReleased);
        grabbable.Grabbed.AddListener(OnGrabbed);

        CreatePreview();
    }
    private void SetMeshConvex(bool convex)
    {
        if (!toggleMeshConvex || meshCols == null) return;
        for (int i = 0; i < meshCols.Length; i++)
        {
            var mc = meshCols[i];
            if (!mc) continue;

            // Catatan: Non-convex tidak boleh trigger
            if (!convex && mc.isTrigger) mc.isTrigger = false;

            mc.convex = convex;
        }
    }

    private void OnDestroy()
    {
        grabbable.Released.RemoveListener(OnReleased);
        grabbable.Grabbed.RemoveListener(OnGrabbed);
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
            // Saat sudah snapped
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

        // Belum snapped: preview hanya tampil kalau lagi dipegang & cukup dekat
        float dist = Vector3.Distance(transform.position, snapTarget.position);

        // HVR 2.9.1h: cek sedang dipegang
        bool isHeld = grabbable.HandGrabbers != null && grabbable.HandGrabbers.Count > 0;

        bool show = isHeld && dist <= snapDistance;
        preview.SetActive(show);

        if (show)
            preview.transform.SetPositionAndRotation(p, r);
    }



    private void OnGrabbed(HVRGrabberBase grabber, HVRGrabbable g)
    {
        if (isSnapped)
        {
            transform.SetParent(null, true);
            rb.isKinematic = false;
            rb.useGravity = true;
            isSnapped = false;            // <<— penting, supaya balik ke mode “dipegang”
            OnUnsnapped?.Invoke();            // <<==== EVENT lepaskan snap

        }
        SetMeshConvex(true);

    }


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
        isSnapped = true;
        SetMeshConvex(false);   // 👉 snap = pakai non-convex
        OnSnapped?.Invoke(snapTarget);        // <<==== EVENT saat berhasil snap


    }

    private void CreatePreview()
    {
        if (!previewPrefab || !snapTarget) return;

        preview = Instantiate(previewPrefab);
        preview.name = "[SnapPreview]";
        preview.SetActive(false);

        // Ambil renderer dari prefab
        var mr = preview.GetComponentInChildren<Renderer>();
        if (!mr) return;

        // Coba pakai URP Unlit (paling sederhana untuk ghost)
        Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");

        var mat = new Material(urpUnlit ? urpUnlit : urpLit);

        // Warna + alpha ghost
        Color c = new Color(0f, 0.6f, 1f, previewFade);
        if (urpUnlit)
        {
            // URP Unlit pakai _BaseColor
            mat.SetColor("_BaseColor", c);
            // Transparent
            mat.SetFloat("_Surface", 1f);                     // 0=Opaque, 1=Transparent
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            // fallback URP Lit
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Surface", 1f);                     // Transparent
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // Non-premultiplied alpha (opsional):
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        // Terapkan material ke renderer (hanya slot 0 agar aman)
        mr.sharedMaterial = mat;

        // Kalau prefab kamu TIDAK punya renderer (mis. cuma Empty), bikin sphere kecil sebagai indikator
        if (!mr)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "[SnapPreviewSphere]";
            var col = sphere.GetComponent<Collider>(); if (col) Destroy(col);
            sphere.transform.SetParent(preview.transform, false);
            sphere.transform.localScale = Vector3.one * 0.06f;

            var sMr = sphere.GetComponent<Renderer>();
            sMr.sharedMaterial = mat;
        }
    }

}
