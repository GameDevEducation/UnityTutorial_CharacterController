using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Equipment/Instant Health", fileName = "Instant Health")]
public class Equipment_InstantHealth : EquipmentBase
{
    [SerializeField] float HealAmount = 20f;

    public override float GetChargesRemaining()
    {
        return 0f;
    }

    public override void OnPickedUp()
    {
        LinkedMotor.OnPerformHeal(LinkedMotor.gameObject, HealAmount);
    }

    public override bool Tick()
    {
        return true;
    }

    public override bool ToggleUse()
    {
        return true;
    }
}
