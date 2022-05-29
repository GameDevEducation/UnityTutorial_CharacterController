using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Equipment/Jetpack", fileName = "Jetpack")]
public class Equipment_Jetpack : EquipmentBase
{
    [SerializeField] float Duration = 30f;

    float DurationUsed = 0f;

    public override bool ToggleUse()
    {
        if (DurationUsed >= Duration)
        {
            if (IsActive)
            {
                IsActive = false;
                OnStopFlying();
            }

            return true;
        }

        IsActive = !IsActive;

        if (IsActive)
            OnStartFlying();
        else
            OnStopFlying();

        return false;
    }

    public override bool Tick()
    {
        if (IsActive)
        {
            DurationUsed += Time.deltaTime;

            // used maximum duration
            if (DurationUsed >= Duration)
            {
                OnStopFlying();
                return true;
            }
        }

        return false;
    }

    CharacterMotor LinkedMotor;
    public override void LinkTo(EquipmentManager manager)
    {
        LinkedMotor = manager.GetComponent<CharacterMotor>();
    }

    protected void OnStartFlying()
    {
        LinkedMotor.SwitchMovementMode<MovementMode_Flying>();
    }

    protected void OnStopFlying()
    {
        LinkedMotor.SwitchMovementMode<MovementMode_Ground>();
    }

    public override float GetFuelRemaining()
    {
        return 1f - (DurationUsed / Duration);
    }
}
