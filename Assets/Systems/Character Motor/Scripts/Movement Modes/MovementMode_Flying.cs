using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementMode_Flying : MonoBehaviour, IMovementMode
{
    protected CharacterMotor.MotorState State;
    protected CharacterMotorConfig Config;
    protected CharacterMotor Motor;

    public float CurrentMaxSpeed
    {
        get
        {
            float speed = Config.RunSpeed;

            return State.CurrentSurfaceSource != null ? State.CurrentSurfaceSource.Effect(speed, EEffectableParameter.Speed) : speed;
        }
        set
        {
            throw new System.NotImplementedException($"CurrentMaxSpeed cannot be set directly. Update the motor config to change speed.");
        }
    }

    public void Initialise(CharacterMotorConfig config, CharacterMotor motor, CharacterMotor.MotorState state)
    {
        Config = config;
        Motor = motor;
        State = state;
    }

    public void FixedUpdate_PreGroundedCheck()
    {
        // align to the local gravity vector
        transform.rotation = Quaternion.FromToRotation(transform.up, State.UpVector) * transform.rotation;
    }

    public RaycastHit FixedUpdate_GroundedCheck()
    {
        State.IsGrounded = false;
        State.IsCrouched = false;

        return new RaycastHit();
    }

    public void FixedUpdate_OnBecameGrounded()
    {

    }

    public void FixedUpdate_TickMovement(RaycastHit groundCheckResult)
    {
        float verticalInput = (State.Input_Jump ? 1f : 0f) + (State.Input_Crouch ? -1f : 0f);
        Vector3 movementInput = Camera.main.transform.forward * State.Input_Move.y +
                                Camera.main.transform.right * State.Input_Move.x +
                                State.UpVector * verticalInput;

        State.LinkedRB.velocity = movementInput * CurrentMaxSpeed;
    }

    public void LateUpdate_Tick()
    {

    }
}
