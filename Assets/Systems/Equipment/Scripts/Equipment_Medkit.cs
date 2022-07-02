using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Equipment/Medkit", fileName = "Medkit")]
public class Equipment_Medkit : EquipmentBase
{
    [SerializeField] int NumCharges = 5;
    [SerializeField] float HealingPerCharge = 20f;

    int NumChargesRemaining;

    public override void OnPickedUp()
    {
        base.OnPickedUp();

        NumChargesRemaining = NumCharges;
    }

    public override float GetChargesRemaining()
    {
        return (float)NumChargesRemaining / NumCharges;
    }

    public override bool Tick()
    {
        return false;
    }

    public override bool ToggleUse()
    {
        if (LinkedMotor.CurrentHealth >= LinkedMotor.MaxHealth)
            return false;

        NumChargesRemaining--;

        LinkedMotor.OnPerformHeal(LinkedMotor.gameObject, HealingPerCharge);

        return NumChargesRemaining == 0;
    }
}
