using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Character Motor/Config", fileName = "CharacterMotorConfig")]
public class CharacterMotorConfig : ScriptableObject
{
    [Header("Character")]
    public float Height = 1.8f;
    public float Radius = 0.3f;

    [Header("Grounded Check")]
    public LayerMask GroundedLayerMask = ~0;
    public float GroundedCheckBuffer = 0.1f;
    public float GroundedCheckRadiusBuffer = 0.05f;

    [Header("Camera")]
    public bool Camera_InvertY = false;
    public float Camera_HorizontalSensitivity = 10f;
    public float Camera_VerticalSensitivity = 10f;
    public float Camera_MinPitch = -75f;
    public float Camera_MaxPitch = 75f;

    [Header("Movement")]
    public float WalkSpeed = 10f;
    public float RunSpeed = 15f;
    public bool CanRun = true;
    public bool IsRunToggle = true;
    public float SlopeLimit = 60f;
}
