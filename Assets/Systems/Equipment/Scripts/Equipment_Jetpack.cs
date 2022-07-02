using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Equipment/Jetpack", fileName = "Jetpack")]
public class Equipment_Jetpack : EquipmentBase
{
    [FormerlySerializedAs("Duration")][SerializeField] float Fuel = 30f;

    float FuelUsed = 0f;

    public override bool ToggleUse()
    {
        if (FuelUsed >= Fuel)
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
            FuelUsed += Time.deltaTime;

            // used maximum duration
            if (FuelUsed >= Fuel)
            {
                OnStopFlying();
                return true;
            }
        }

        return false;
    }

    protected void OnStartFlying()
    {
        LinkedMotor.SwitchMovementMode<MovementMode_Flying>();
    }

    protected void OnStopFlying()
    {
        LinkedMotor.SwitchMovementMode<MovementMode_Ground>();
    }

    public override float GetChargesRemaining()
    {
        return 1f - (FuelUsed / Fuel);
    }
}
