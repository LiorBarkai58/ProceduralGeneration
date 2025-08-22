using System;
using UnityEngine;
using UnityEngine.AI;

public class Portal : MonoBehaviour
{
    public Vector3 TargetPosition;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            print("teleported");
            if (other.transform.parent.TryGetComponent(out NavMeshAgent agent))
            {
                agent.Warp(TargetPosition);
                
            }
        }
    }
}