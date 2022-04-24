using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AICharacterMotor : CharacterMotor
{
    [Header("AI")]
    [SerializeField, Range(0f, 1f)] float DesiredVelocityWeighting = 0.5f;
    [SerializeField] float MaxAngleToPermitMovement = 30f;
    [SerializeField] AnimationCurve SpeedScaleVsRotationPriority;
    [SerializeField] float MaxAngleToTreatAsLookingAt = 5f;

    public void SteerTowards(Vector3 target, float rotationSpeed, float stoppingdistance, float speed)
    {
        // get the vector to the target
        Vector3 vecToTarget = target - transform.position;
        vecToTarget.y = 0f;

        // determine our velocities
        Vector3 desiredVelocity = vecToTarget.normalized * speed;
        Vector3 targetVelocity = Vector3.Lerp(LinkedRB.velocity, desiredVelocity, DesiredVelocityWeighting);

        // get the angle between our current facing and the target facing
        float angleDelta = Mathf.Acos(Vector3.Dot(targetVelocity.normalized, transform.forward)) * Mathf.Rad2Deg;

        // are we needing to turn too far?
        float movementScale = 0f;
        float rotationPriority = 1f;
        if (angleDelta < MaxAngleToPermitMovement)
        {
            rotationPriority = Mathf.Abs(angleDelta) / MaxAngleToPermitMovement;
            movementScale = SpeedScaleVsRotationPriority.Evaluate(rotationPriority);
        }

        // determine and apply the rotation required
        float rotationRequired = Mathf.Sign(Vector3.Dot(targetVelocity, transform.right)) * rotationSpeed * Time.deltaTime;
        rotationRequired *= rotationPriority;
        transform.localRotation = transform.localRotation * Quaternion.AngleAxis(rotationRequired, transform.up);

        // calculate the movement input
        _Input_Move.y = movementScale * Mathf.Clamp(Vector3.Dot(targetVelocity, transform.forward) / CurrentMaxSpeed, -1f, 1f);
        _Input_Move.x = movementScale * Mathf.Clamp(Vector3.Dot(targetVelocity, transform.right) / CurrentMaxSpeed, -1f, 1f);
    }

    public void Stop()
    {
        _Input_Move = Vector2.zero;
    }

    public bool LookTowards(Transform target, float rotationSpeed)
    {
        // get the 2D vector to the target
        Vector3 vecToTarget = target.position - transform.position;
        vecToTarget.y = 0f;
        vecToTarget.Normalize();

        // are we already looking at the target?
        float angleToTarget = Mathf.Acos(Vector3.Dot(vecToTarget, transform.forward)) * Mathf.Rad2Deg;
        if (angleToTarget <= MaxAngleToTreatAsLookingAt)
            return true;

        // look towards the target
        float rotationRequired = Mathf.Sign(Vector3.Dot(vecToTarget, transform.right)) * rotationSpeed * Time.deltaTime;
        transform.localRotation = transform.localRotation * Quaternion.AngleAxis(rotationRequired, transform.up);

        return false;
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();

        UpdateCamera();
    }

    protected void UpdateCamera()
    {
        // not allowed to look around?
        if (IsLookingLocked)
            return;

        // allow surface to effect sensitivity
        float hSensitivity = Config.Camera_HorizontalSensitivity;
        if (CurrentSurfaceSource != null)
        {
            hSensitivity = CurrentSurfaceSource.Effect(hSensitivity, EEffectableParameter.CameraSensitivity);
        }

        // calculate our camera inputs
        float cameraYawDelta = _Input_Look.x * hSensitivity * Time.deltaTime;

        // rotate the character
        transform.localRotation = transform.localRotation * Quaternion.Euler(0f, cameraYawDelta, 0f);
    }
}
