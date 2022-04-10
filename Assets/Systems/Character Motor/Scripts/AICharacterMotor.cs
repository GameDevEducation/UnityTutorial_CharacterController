using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AICharacterMotor : CharacterMotor
{
    public void SetTurnRate(float rate)
    {
        _Input_Look.x = rate;
    }

    public void SetMovement(float speed)
    {
        _Input_Move.y = speed;
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
