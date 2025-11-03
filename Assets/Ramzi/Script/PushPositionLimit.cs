using UnityEngine;

public class PushPositionLimit : MonoBehaviour
{
    [Header("Aktifkan sumbu yang dibatasi")]
    public bool limitX = false;
    public bool limitY = false;
    public bool limitZ = true;

    [Header("Batas gerak relatif terhadap posisi awal")]
    public float minX = -1f;
    public float maxX = 1f;
    public float minY = 0f;
    public float maxY = 0.5f;
    public float minZ = -1f;
    public float maxZ = 1f;

    private Vector3 startPos;
    private Rigidbody rb;


    

    public bool IsLimit = false;
    void Start()
    {
        startPos = transform.position;
        rb = GetComponent<Rigidbody>();
        Debug.Log($"[PushLimit] StartPos = {startPos}");
    }

    void LateUpdate()
    {
        Vector3 pos = transform.position;
        Vector3 vel = rb ? rb.velocity : Vector3.zero;

        bool snapped = false;

        // === X ===
        if (limitX)
        {
            float offsetX = pos.x - startPos.x;
            if (offsetX < minX)
            {
                pos.x = startPos.x + minX;
                if (rb) rb.velocity = new Vector3(0, vel.y, vel.z);
                Debug.Log($"[PushLimit] Snap ke MIN X ({pos.x})");
                snapped = true;
            }
            else if (offsetX > maxX)
            {
                pos.x = startPos.x + maxX;
                if (rb) rb.velocity = new Vector3(0, vel.y, vel.z);
                Debug.Log($"[PushLimit] Snap ke MAX X ({pos.x})");
                snapped = true;
            }
        }

        // === Y ===
        if (limitY)
        {
            float offsetY = pos.y - startPos.y;
            if (offsetY < minY)
            {
                pos.y = startPos.y + minY;
                if (rb) rb.velocity = new Vector3(vel.x, 0, vel.z);
                Debug.Log($"[PushLimit] Snap ke MIN Y ({pos.y})");
                snapped = true;
            }
            else if (offsetY > maxY)
            {
                pos.y = startPos.y + maxY;
                if (rb) rb.velocity = new Vector3(vel.x, 0, vel.z);
                Debug.Log($"[PushLimit] Snap ke MAX Y ({pos.y})");
                snapped = true;
            }
        }

        // === Z ===
        if (limitZ)
        {
            float offsetZ = pos.z - startPos.z;
            if (offsetZ < minZ)
            {
                rb.isKinematic = true;

                pos.z = startPos.z + minZ;
                if (rb) rb.velocity = new Vector3(vel.x, vel.y, 0);
                Debug.Log($"[PushLimit] Snap ke MIN Z ({pos.z})");
                IsLimit = true;
                snapped = true;
            }
            else if (offsetZ > maxZ)
            {
                rb.isKinematic = false;

                pos.z = startPos.z + maxZ;
                if (rb) rb.velocity = new Vector3(vel.x, vel.y, 0);
                Debug.Log($"[PushLimit] Snap ke MAX Z ({pos.z})");
                snapped = true;
            }
        }

        transform.position = pos;

        if (snapped)
        {
            Debug.DrawLine(transform.position, transform.position + Vector3.up * 0.5f, Color.red, 0.1f);
        }
    }
}
