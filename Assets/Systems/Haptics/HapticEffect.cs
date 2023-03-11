using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Haptic Effect Config", menuName = "Haptics/Effect Config", order = 1)]
public class HapticEffect : ScriptableObject
{
    public enum EType
    {
        SingleValue,
        Curve
    }

    public enum EBlendMode
    {
        Overwrite,
        Add,
        Subtract,
        Multiply
    }

    [Header("Common Settings")]
    public EType Type = EType.Curve;
    public EBlendMode Blending = EBlendMode.Add;
    public float Duration = 1f;
    public bool Looping = false;
    
    [Header("Gamepad")]
    public AnimationCurve Gamepad_LowFrequencyMotor_Curve;
    public AnimationCurve Gamepad_HighFrequencyMotor_Curve;
    public float Gamepad_LowFrequencyMotor_Value = -1f;
    public float Gamepad_HighFrequencyMotor_Value = -1f;

    public bool Validate(float activeDuration)
    {
        if (activeDuration < 0 && Type == EType.Curve)
            return false;

        return true;
    }
}
