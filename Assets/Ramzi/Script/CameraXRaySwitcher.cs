using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class MultiCameraMaterial : MonoBehaviour
{
    public Camera camera1;
    public Camera camera2;
    public Material materialXRay;

    private Material originalMat;
    private Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
        originalMat = rend.sharedMaterial;
    }

    void OnWillRenderObject()
    {
        if (Camera.current == camera2)
            rend.sharedMaterial = materialXRay;
        else
            rend.sharedMaterial = originalMat;
    }
}
