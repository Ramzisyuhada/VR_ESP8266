using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RaycastLine : MonoBehaviour
{
    public float distance = 20f;

    private LineRenderer line;

    void Start()
    {
        line = GetComponent<LineRenderer>();

        line.positionCount = 2;
        line.startWidth = 0.01f;
        line.endWidth = 0.01f;
    }

    void Update()
    {
        // Posisi dan arah dari controller / tangan VR
        Vector3 startPos = transform.position;
        Vector3 direction = transform.forward;

        RaycastHit hit;

        Vector3 endPos;

        // Raycast
        if (Physics.Raycast(startPos, direction, out hit, distance))
        {
            endPos = hit.point;

            Debug.Log("Hit: " + hit.collider.name);
        }
        else
        {
            endPos = startPos + direction * distance;
        }

        // Gambar laser
        line.SetPosition(0, startPos);
        line.SetPosition(1, endPos);
    }
}