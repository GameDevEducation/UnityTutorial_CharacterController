using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PickupScanner : MonoBehaviour
{
    [SerializeField] float DetectionInterval = 0.1f;
    [SerializeField] float DetectionRange = 2.5f;
    [SerializeField] LayerMask DetectionMask = ~0;
    [SerializeField] Camera OverrideCamera;

    float NextDetectionTime = 0f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (NextDetectionTime <= 0f)
        {
            NextDetectionTime = DetectionInterval;
            RunPickupCheck();
        }
        else
            NextDetectionTime -= DetectionInterval;
    }

    Pickup CurrentPickupTarget = null;

    void RunPickupCheck()
    {
        var workingCamera = OverrideCamera != null ? OverrideCamera : Camera.main;

        // check if we can hit anything
        RaycastHit hitResult;
        bool foundPickup = false;
        if (Physics.Raycast(workingCamera.transform.position, workingCamera.transform.forward, out hitResult,
                            DetectionRange, DetectionMask, QueryTriggerInteraction.Collide))
        {
            Pickup pickupLogic = null;
            if (hitResult.collider.TryGetComponent<Pickup>(out pickupLogic))
            {
                foundPickup = true;

                if (pickupLogic.HasPickupPrompt)
                {
                    // pickup target has changed
                    if (pickupLogic != CurrentPickupTarget)
                    {
                        pickupLogic.OnStartLookingAt();

                        if (CurrentPickupTarget != null)
                            CurrentPickupTarget.OnStopLookingAt();

                        CurrentPickupTarget = pickupLogic;
                    }
                    else
                        CurrentPickupTarget.OnContinueLookingAt();
                }
            }
        }

        if (!foundPickup && CurrentPickupTarget != null)
        {
            CurrentPickupTarget.OnStopLookingAt();
            CurrentPickupTarget = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Pickup pickupLogic = null;
        if (other.TryGetComponent<Pickup>(out pickupLogic))
        {
            if (pickupLogic.PickupOnContact && pickupLogic.CanPickup())
            {
                pickupLogic.PerformPickup();
            }
        }
    }

    protected virtual void OnPickup(InputValue value)
    {
        if (value.isPressed && CurrentPickupTarget && CurrentPickupTarget.CanPickup() && !CurrentPickupTarget.PickupOnContact)
        {
            CurrentPickupTarget.PerformPickup();
        }
    }
}
