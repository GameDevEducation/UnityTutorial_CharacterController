using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterUI_StaminaBar : MonoBehaviour
{
    [SerializeField] RectTransform StaminaBarTransform;
    [SerializeField] Image StaminaBarImage;
    [SerializeField] Gradient StaminaBarGradient;

    protected float MaxBarLength;

    // Start is called before the first frame update
    void Start()
    {
        MaxBarLength = StaminaBarTransform.rect.width;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnStaminaChanged(float currentStamina, float maxStamina)
    {
        float staminaPercentage = currentStamina / maxStamina;
        float newLength = MaxBarLength * staminaPercentage;

        StaminaBarTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newLength);
        StaminaBarImage.color = StaminaBarGradient.Evaluate(staminaPercentage);
    }
}
