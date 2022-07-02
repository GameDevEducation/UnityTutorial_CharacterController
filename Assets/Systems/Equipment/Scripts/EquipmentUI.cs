using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EquipmentUI : MonoBehaviour
{
    [SerializeField] Image EquipmentImage;
    [SerializeField] TextMeshProUGUI EquipmentName;
    [SerializeField] Color InUseColour = Color.green;
    [SerializeField] Color NotInUseColour = Color.white;
    [SerializeField] Slider EquipmentTimeRemainingSlider;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetEquipment(EquipmentBase equipment)
    {
        EquipmentImage.sprite = equipment.Icon;
        EquipmentName.text = equipment.DisplayName;

        SetEquipmentInUse(equipment.IsActive);
        SetEquipmentTimeRemaining(equipment.HasCharges, equipment.GetChargesRemaining());
    }

    public void SetEquipmentInUse(bool inUse)
    {
        EquipmentImage.color = inUse ? InUseColour : NotInUseColour;
    }

    public void SetEquipmentTimeRemaining(bool show, float timeRemaining)
    {
        if (show)
        {
            EquipmentTimeRemainingSlider.gameObject.SetActive(true);
            EquipmentTimeRemainingSlider.SetValueWithoutNotify(timeRemaining);
        }
        else
            EquipmentTimeRemainingSlider.gameObject.SetActive(false);
    }
}
