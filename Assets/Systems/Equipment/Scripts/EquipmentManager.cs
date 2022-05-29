using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class EquipmentManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] EquipmentBase DEBUG_EquipmentToAdd;
    [SerializeField] bool DEBUG_AddEquipment;

    protected EquipmentUI LinkedUI;

    List<EquipmentBase> AllEquipment = new List<EquipmentBase>();
    int CurrentEquipmentIndex = -1;

    public EquipmentBase CurrentEquipment
    {
        get
        {
            if (CurrentEquipmentIndex < 0 || CurrentEquipmentIndex >= AllEquipment.Count)
                return null;

            return AllEquipment[CurrentEquipmentIndex];
        }
    }
    
    // Start is called before the first frame update
    void Start()
    {
        LinkedUI = FindObjectOfType<EquipmentUI>();
        SelectEquipment(CurrentEquipmentIndex);
    }

    // Update is called once per frame
    void Update()
    {
        if (DEBUG_AddEquipment && DEBUG_EquipmentToAdd != null)
        {
            DEBUG_AddEquipment = false;

            AddEquipment(DEBUG_EquipmentToAdd);
        }

        if (CurrentEquipment != null)
        {
            if (CurrentEquipment.Tick())
                RemoveEquipment(CurrentEquipment);

            if (CurrentEquipment != null)
            {
                LinkedUI.SetEquipmentInUse(CurrentEquipment.IsActive);
                LinkedUI.SetEquipmentTimeRemaining(CurrentEquipment.HasFuel, CurrentEquipment.GetFuelRemaining());
            }
        }
    }

    public void AddEquipment(EquipmentBase equipment)
    {
        var newEquipment = ScriptableObject.Instantiate(equipment);

        newEquipment.LinkTo(this);
        AllEquipment.Add(newEquipment);

        if (CurrentEquipmentIndex == -1)
            SelectEquipment(0);
    }

    protected void RemoveEquipment(EquipmentBase equipment)
    {
        var wasActive = CurrentEquipmentIndex == AllEquipment.IndexOf(equipment);

        AllEquipment.Remove(equipment);

        if (wasActive)
        {
            if (AllEquipment.Count == 0)
                SelectEquipment(-1);
            else
                SelectEquipment(CurrentEquipmentIndex % AllEquipment.Count);
        }
    }

    protected void SelectEquipment(int newIndex)
    {
        CurrentEquipmentIndex = newIndex;

        if (CurrentEquipmentIndex < 0 || CurrentEquipmentIndex >= AllEquipment.Count)
        {
            LinkedUI.gameObject.SetActive(false);
        }
        else
        {
            LinkedUI.gameObject.SetActive(true);
            LinkedUI.SetEquipment(CurrentEquipment);
        }
    }

    protected void OnPreviousEquipment(InputValue value)
    {
        if (!value.isPressed)
            return;

        SelectEquipment((CurrentEquipmentIndex - 1 + AllEquipment.Count) % AllEquipment.Count);
    }

    protected void OnNextEquipment(InputValue value)
    {
        if (!value.isPressed)
            return;

        SelectEquipment((CurrentEquipmentIndex + 1) % AllEquipment.Count);
    }

    protected void OnUseEquipment(InputValue value)
    {
        if (!value.isPressed)
            return;

        if (CurrentEquipmentIndex < 0 || CurrentEquipmentIndex >= AllEquipment.Count)
        {
            // do nothing - could show tutorial message
            return;
        }

        var equipmentToUse = AllEquipment[CurrentEquipmentIndex];
        if (equipmentToUse.ToggleUse())
        {
            RemoveEquipment(equipmentToUse);
        }
    }
}
