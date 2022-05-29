using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pickup_Equipment : MonoBehaviour
{
    [SerializeField] EquipmentBase EquipmentToAdd;

    public void OnPickedUp()
    {
        FindObjectOfType<EquipmentManager>().AddEquipment(EquipmentToAdd);
    }
}
