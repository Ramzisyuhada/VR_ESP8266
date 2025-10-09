using UnityEngine;
using System;

[RequireComponent(typeof(Collider))]
public class ZoneGoalDetector : MonoBehaviour
{
    public string PlayerTag = "Player";
    public bool Once = true;
    public bool TriggerIfAlreadyInside = true;

    public event Action<ZoneGoalDetector> OnEntered;  // <- tipe generik

    private bool _triggered;
    private Collider _zone;

    void Awake()
    {
        _zone = GetComponent<Collider>();
        if (_zone) _zone.isTrigger = true;
    }

    void OnEnable()
    {
        if (TriggerIfAlreadyInside)
        {
            var player = GameObject.FindWithTag(PlayerTag);
            if (player && _zone && _zone.bounds.Contains(player.transform.position))
            {
                Fire();
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // (opsional) filter agar yang dihitung hanya root player,
        // tidak semua collider jari/tangan
        var root = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;
        if (_triggered && Once) return;
        if (!root.CompareTag(PlayerTag)) return;

        Fire();
    }

    private void Fire()
    {
        if (_triggered && Once) return;
        _triggered = true;

        OnEntered?.Invoke(this);          // <- PENTING: kirim "this"

        if (Once) enabled = false;
    }
}
