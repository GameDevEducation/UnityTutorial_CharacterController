using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class EquipmentBase : ScriptableObject
{
    public string DisplayName;
    public Sprite Icon;
    public bool AddToInventory = true;
    [FormerlySerializedAs("HasFuel")] public bool HasCharges;

    public bool IsActive { get; protected set; } = false;

    protected CharacterMotor LinkedMotor;

    public abstract bool ToggleUse();
    public abstract bool Tick();

    public abstract float GetChargesRemaining();

    public virtual void LinkTo(EquipmentManager manager)
    {
        LinkedMotor = manager.GetComponent<CharacterMotor>();
    }

    public virtual void OnPickedUp() { }
}
