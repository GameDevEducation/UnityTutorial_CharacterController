using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class EquipmentBase : ScriptableObject
{
    public string DisplayName;
    public Sprite Icon;
    public bool HasFuel;

    public abstract bool ToggleUse();
    public abstract bool Tick();

    public abstract void LinkTo(EquipmentManager manager);
    public abstract float GetFuelRemaining();

    public bool IsActive { get; protected set; } = false;
}
