using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HVRPushBooster : MonoBehaviour
{
    [Header("Kekuatan Dorong")]
    public float pushForce = 6f;

    [Header("Minimal kecepatan tangan supaya ngedorong")]
    public float minHandVelocity = 0.2f;

    private Rigidbody handRb;

    private void Awake()
    {
        // pastikan tangan punya rigidbody
        handRb = GetComponent<Rigidbody>();
        if (handRb == null)
        {
            handRb = gameObject.AddComponent<Rigidbody>();
            handRb.isKinematic = true;
            handRb.useGravity = false;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        var otherRb = collision.rigidbody;
        if (otherRb == null || otherRb.isKinematic) return;

        if (handRb.velocity.magnitude < minHandVelocity) return;

        Vector3 pushDir = handRb.velocity.normalized;
        otherRb.AddForce(pushDir * pushForce, ForceMode.Impulse);
    }
}
