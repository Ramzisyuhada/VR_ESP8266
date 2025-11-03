using UnityEngine;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Grabbers;
using System;

[RequireComponent(typeof(HVRGrabbable))]
[RequireComponent(typeof(Rigidbody))]
public class SnapBase : MonoBehaviour
{
    [Header("Snap Target")]
    public Transform snapTarget;
    public float snapDistance = 0.25f;
    public bool parentToTarget = true;

    [Header("Visual Preview")]
    public GameObject previewPrefab;
    public Vector3 positionOffset;
    public Vector3 rotationOffsetEuler;
    public Color previewColor = new Color(0f, 0.6f, 1f, 0.35f);
    public Color readyColor = new Color(0.2f, 1f, 0.2f, 0.5f);
    public bool keepPreviewWhenSnapped = true;

    [Header("Startup Behavior")]
    public bool snapOnStart = true;
    public bool keepConvexWhenSnapOnStart = true;

    [Header("Convex Control")]
    public bool autoToggleConvex = true;

    protected HVRGrabbable grabbable;
    protected Rigidbody rb;
    protected bool isSnapped = false;
    protected GameObject preview;
    protected Renderer previewRenderer;
    protected Material previewMat;
    protected MeshCollider[] meshCols;
    protected bool useURPUnlit = false;
    public bool IsSnapped
    {
        get => isSnapped;
        set => isSnapped = value;
    }
    public event Action<Transform> OnSnapped;
    public event Action OnUnsnapped;

    [Header("Optional Audio")]
    public AudioSource audio;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabbable = GetComponent<HVRGrabbable>();
        meshCols = GetComponentsInChildren<MeshCollider>(true);

        grabbable.Released.AddListener(OnReleased);
        grabbable.Grabbed.AddListener(OnGrabbed);

        CreatePreview();
        SetMeshConvex(true);
    }

    protected virtual void Start()
    {
        if (snapOnStart && snapTarget)
        {
            SnapToTarget(true);
        }
    }

    protected virtual void Update()
    {
        if (!snapTarget || !preview) return;

        Vector3 p = snapTarget.TransformPoint(positionOffset);
        Quaternion r = snapTarget.rotation * Quaternion.Euler(rotationOffsetEuler);

        float dist = Vector3.Distance(transform.position, snapTarget.position);
        bool ready = dist <= snapDistance;
        bool isHeld = grabbable.HandGrabbers != null && grabbable.HandGrabbers.Count > 0;

        preview.SetActive(isHeld || keepPreviewWhenSnapped);
        if (!preview.activeSelf) return;

        preview.transform.SetPositionAndRotation(p, r);
        SetPreviewColor((isHeld && ready) ? readyColor : previewColor);
    }

    protected virtual void OnGrabbed(HVRGrabberBase grabber, HVRGrabbable g)
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
    }

    protected virtual void OnReleased(HVRGrabberBase grabber, HVRGrabbable g)
    {
        if (!snapTarget) return;

        float dist = Vector3.Distance(transform.position, snapTarget.position);
        if (dist <= snapDistance)
        {
            SnapToTarget(false);
        }
    }

    protected virtual void SnapToTarget(bool isInitial)
    {
        Vector3 pos = snapTarget.TransformPoint(positionOffset);
        Quaternion rot = snapTarget.rotation * Quaternion.Euler(rotationOffsetEuler);

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;

        transform.SetPositionAndRotation(pos, rot);
        if (parentToTarget) transform.SetParent(snapTarget);

        if (isInitial && keepConvexWhenSnapOnStart)
            SetMeshConvex(true);
        else
            SetMeshConvex(false);

        isSnapped = true;
        OnSnapped?.Invoke(snapTarget);
        if (audio) audio.Play();

        if (preview) preview.SetActive(keepPreviewWhenSnapped);
    }

    protected virtual void SetMeshConvex(bool convex)
    {
        if (!autoToggleConvex || meshCols == null) return;
        foreach (var mc in meshCols)
        {
            if (!mc) continue;
            mc.convex = convex;
        }
    }

    protected virtual void CreatePreview()
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
        SetPreviewColor(previewColor);

        previewMat.SetFloat("_Surface", 1f);
        previewMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        previewMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        previewRenderer.sharedMaterial = previewMat;
    }

    protected virtual void SetPreviewColor(Color color)
    {
        if (!previewRenderer) return;
        var mats = previewRenderer.materials;
        foreach (var mat in mats)
        {
            if (!mat) continue;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
        }
    }
}
