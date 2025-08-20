using System;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerController : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private CinemachinePositionComposer CM_PositionComposer;
    [SerializeField] private Camera MainCamera;
    [SerializeField] private GameObject Pointer;
    [Header("Camera")]
    [SerializeField,Range(3,100)] private float MinCameraDistance;
    [SerializeField,Range(3,100)] private float MaxCameraDistance;
    [Header("Navigation")]
    [SerializeField] private float MovementSpeed = 3;
    [SerializeField] private LayerMask GroundLayer;

    private NavMeshAgent agent;
    private GameObject pointerRef;
    private void OnValidate()
    {
        if (agent != null) agent.speed = MovementSpeed;
        if (MinCameraDistance > MaxCameraDistance)
        {
            MinCameraDistance = MaxCameraDistance - 1;
        }

    }
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = MovementSpeed;
        Cursor.lockState = CursorLockMode.Confined;
    }

    public void OnZoom(InputAction.CallbackContext context)
    {
        float scrollValue = context.ReadValue<Vector2>().y;

        if(CheckForCeiling(scrollValue)) return;

        CM_PositionComposer.CameraDistance -= scrollValue;
        CM_PositionComposer.CameraDistance = Mathf.Clamp(CM_PositionComposer.CameraDistance, MinCameraDistance,  MaxCameraDistance);

    }

    private void Update()
    {

        if (agent.pathPending && agent.remainingDistance < agent.stoppingDistance && agent.velocity == Vector3.zero)
        {
            DeletePointer();
        }
    }
    public void OnGo(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Ray ray = MainCamera.ScreenPointToRay(Input.mousePosition);        

            if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, GroundLayer))
            {
                Vector3 worldPosition = hitInfo.point;

                agent.destination = worldPosition;
                DeletePointer();
                pointerRef = Instantiate(Pointer, worldPosition,Quaternion.identity);
            }

        } 

    }

    private void DeletePointer()
    {
        if (pointerRef)
        {
            Destroy(pointerRef);
            pointerRef = null;
        }
    }

    private bool CheckForCeiling(float scrollValue)
    {

        Vector3 startPos = MainCamera.transform.position;
        Vector3 direction = Vector3.up;
        float range = 1;

        if (Physics.Raycast(startPos, direction, range, GroundLayer) && scrollValue < 0)
        {
            return true;
        }
        else return false;
    }
}

