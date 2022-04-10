using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterUI_HealthBar : MonoBehaviour
{
    [SerializeField] RectTransform HealthBarTransform;
    [SerializeField] Image HealthBarImage;
    [SerializeField] Gradient HealthBarGradient;

    protected float MaxBarLength;

    // Start is called before the first frame update
    void Start()
    {
        if (HealthBarTransform.rect.width > 0)
            MaxBarLength = HealthBarTransform.rect.width;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnHealthChanged(float currentHealth, float maxHealth)
    {
        if (MaxBarLength == 0)
            MaxBarLength = HealthBarTransform.rect.width;

        float healthPercentage = currentHealth / maxHealth;
        float newLength = MaxBarLength * healthPercentage;

        HealthBarTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newLength);
        HealthBarImage.color = HealthBarGradient.Evaluate(healthPercentage);
    }
}
