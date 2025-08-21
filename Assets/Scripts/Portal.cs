using System;
using UnityEngine;

public class Portal : MonoBehaviour
{
    public Vector3 TargetPosition;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.position = TargetPosition;
        }
    }
}