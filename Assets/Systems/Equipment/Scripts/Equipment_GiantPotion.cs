using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Equipment/Potions/Giant", fileName = "Potion_Giant")]
public class Equipment_GiantPotion : EquipmentBase
{
    [SerializeField] float Duration = 30f;
    [SerializeField] float HeightMultiplier = 2f;
    [SerializeField] float JumpHeightMultiplier = 1f;

    public class HeightEffector : CharacterMotor.IParameterEffector
    {
        float Multiplier;
        float Duration;

        public float Effect(float currentValue)
        {
            return currentValue * Multiplier;
        }

        public CharacterMotor.EParameter GetEffectedParameter()
        {
            return CharacterMotor.EParameter.Height;
        }

        public bool Tick(float deltaTime)
        {
            Duration -= deltaTime;

            return Duration <= 0f;
        }

        public HeightEffector(float multiplier, float duration)
        {
            Multiplier = multiplier;
            Duration = duration;    
        }
    }

    public class JumpHeightEffector : CharacterMotor.IParameterEffector
    {
        float Multiplier;
        float Duration;

        public float Effect(float currentValue)
        {
            return currentValue * Multiplier;
        }

        public CharacterMotor.EParameter GetEffectedParameter()
        {
            return CharacterMotor.EParameter.JumpHeight;
        }

        public bool Tick(float deltaTime)
        {
            Duration -= deltaTime;

            return Duration <= 0f;
        }

        public JumpHeightEffector(float multiplier, float duration)
        {
            Multiplier = multiplier;
            Duration = duration;
        }
    }

    public override void OnPickedUp()
    {
        base.OnPickedUp();

        LinkedMotor.AddParameterEffector(new HeightEffector(HeightMultiplier, Duration));
        LinkedMotor.AddParameterEffector(new JumpHeightEffector(JumpHeightMultiplier, Duration));
    }

    public override float GetChargesRemaining()
    {
        return 0f;
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
