#define HAPTICS_CINEMACHINE_SUPPORT

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class HapticsManager : MonoBehaviour
{
    protected class HapticEffectData
    {
        public System.Guid PlayingID { get; private set; }
        public HapticEffect Effect { get; private set; }
        public HapticEffect.EBlendMode Blending { get; private set; }
        public float Duration { get; private set; }
        public float TimeRemaining { get; private set; }
        public bool Looping { get; private set; }
        public float IntensityMultiplier { get; private set; }

        public HapticEffectData(HapticEffect _effect, float _duration, HapticEffect.EBlendMode _blending, bool _looping)
        {
            PlayingID = System.Guid.NewGuid();
            Effect = _effect;
            TimeRemaining = Duration = _duration;
            Blending = _blending;
            Looping = _looping;
            IntensityMultiplier = 1f;
        }

        public bool AdvanceTime(float amount)
        {
            TimeRemaining -= amount;

            if (Looping && TimeRemaining <= 0)
                TimeRemaining += Duration;

            return TimeRemaining <= 0;
        }

        public void SetDuration(float newDuration)
        {
            Duration = newDuration;
            if (TimeRemaining > newDuration)
                TimeRemaining = newDuration;
        }

        public void SetIntensityMultiplier(float newIntensityMultiplier)
        {
            IntensityMultiplier = newIntensityMultiplier;
        }

        public void ApplyTo(ref float lowFrequencyMotor, ref float highFrequencyMotor)
        {
            float baseValue_LowFreq = -1f;
            float baseValue_HighFreq = -1f;

            // single value mode?
            if (Effect.Type == HapticEffect.EType.SingleValue)
            {
                baseValue_LowFreq = Effect.Gamepad_LowFrequencyMotor_Value;
                baseValue_HighFreq = Effect.Gamepad_HighFrequencyMotor_Value;
            } // curve mode
            else
            {
                float progress = TimeRemaining / Duration;

                if (Effect.Gamepad_LowFrequencyMotor_Curve.keys.Length > 0)
                    baseValue_LowFreq = Effect.Gamepad_LowFrequencyMotor_Curve.Evaluate(progress);
                if (Effect.Gamepad_HighFrequencyMotor_Curve.keys.Length > 0)
                    baseValue_HighFreq = Effect.Gamepad_HighFrequencyMotor_Curve.Evaluate(progress);
            }

            baseValue_LowFreq *= IntensityMultiplier;
            baseValue_HighFreq *= IntensityMultiplier;

            // blend the values in
            switch (Blending)
            {
                case HapticEffect.EBlendMode.Overwrite:
                {
                    if (baseValue_LowFreq > -0.9f)
                        lowFrequencyMotor = baseValue_LowFreq;
                    if (baseValue_HighFreq > -0.9f)
                        highFrequencyMotor = baseValue_HighFreq;
                }
                break;

                case HapticEffect.EBlendMode.Add:
                {
                    if (baseValue_LowFreq > -0.9f)
                        lowFrequencyMotor += baseValue_LowFreq;
                    if (baseValue_HighFreq > -0.9f)
                        highFrequencyMotor += baseValue_HighFreq;
                }
                break;

                case HapticEffect.EBlendMode.Subtract:
                {
                    if (baseValue_LowFreq > -0.9f)
                        lowFrequencyMotor -= baseValue_LowFreq;
                    if (baseValue_HighFreq > -0.9f)
                        highFrequencyMotor -= baseValue_HighFreq;
                }
                break;

                case HapticEffect.EBlendMode.Multiply:
                {
                    if (baseValue_LowFreq > -0.9f)
                        lowFrequencyMotor *= baseValue_LowFreq;
                    if (baseValue_HighFreq > -0.9f)
                        highFrequencyMotor *= baseValue_HighFreq;
                }
                break;                                                
            }
        }
    }

    protected List<HapticEffectData> ActiveEffects = new List<HapticEffectData>();

    public static HapticsManager Instance { get; private set; }

    private bool EnableHaptics = true;

#if HAPTICS_CINEMACHINE_SUPPORT
    [Header("Cinemachine Integration")]
    [SerializeField] bool ListenForCinemachineImpulses = false;
    [SerializeField] GameObject ImpulseTarget;

    /// <summary>
    /// Impulse events on channels not included in the mask will be ignored.
    /// </summary>
    [Tooltip("Impulse events on channels not included in the mask will be ignored.")]
    [Cinemachine.CinemachineImpulseChannelProperty]
    [SerializeField] int ChannelMask;

    /// <summary>
    /// Gain to apply to the Impulse signal.
    /// </summary>
    [Tooltip("Gain to apply to the Impulse signal.  1 is normal strength.  Setting this to 0 completely mutes the signal.")]
    [SerializeField] float Gain;

    /// <summary>
    /// Enable this to perform distance calculation in 2D (ignore Z).
    /// </summary>
    [Tooltip("Enable this to perform distance calculation in 2D (ignore Z)")]
    [SerializeField] bool Use2DDistance;

    [SerializeField] [Range(0f, 1f)] float Impulse_CurrentFrameWeight = 0.5f;
    [SerializeField] [Range(0f, 1f)] float Impulse_DeltaFrameWeight = 0.5f;

    [SerializeField] AnimationCurve Impulse_LowFrequencyWeighting;
    [SerializeField] AnimationCurve Impulse_HighFrequencyWeighting;
    
    private Vector3 ImpulsePosLastFrame;
    private Quaternion ImpulseRotLastFrame;
#endif // HAPTICS_CINEMACHINE_SUPPORT

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
    }

    private void OnEnable()
    {
#if HAPTICS_CINEMACHINE_SUPPORT
        if (ListenForCinemachineImpulses)
        {
            ImpulsePosLastFrame = Vector3.zero;
            ImpulseRotLastFrame = Quaternion.identity;
        }
#endif // HAPTICS_CINEMACHINE_SUPPORT
    }
    
    // Update is called once per frame
    void Update()
    {
        // update gamepad haptics
        if(Gamepad.current != null)
        {
            Update_Internal_Gamepad();

#if HAPTICS_CINEMACHINE_SUPPORT
            if (ListenForCinemachineImpulses && EnableHaptics)
            {
                Vector3 impulsePosThisFrame;
                Quaternion impulseRotThisFrame;

                // retrieve the shake
                if (Cinemachine.CinemachineImpulseManager.Instance.GetImpulseAt(ImpulseTarget.transform.position, Use2DDistance, ChannelMask, out impulsePosThisFrame, out impulseRotThisFrame))
                {
                    impulsePosThisFrame *= Gain;
                    impulseRotThisFrame = Quaternion.SlerpUnclamped(Quaternion.identity, impulseRotThisFrame, -Gain);

                    float intensity = Mathf.Lerp(0, impulsePosThisFrame.magnitude, Impulse_CurrentFrameWeight) + 
                                      Mathf.Lerp(0, Vector3.Distance(ImpulsePosLastFrame, impulsePosThisFrame), Impulse_DeltaFrameWeight);

                    Gamepad.current.SetMotorSpeeds(Impulse_LowFrequencyWeighting.Evaluate(intensity), Impulse_HighFrequencyWeighting.Evaluate(intensity));

                    ImpulsePosLastFrame = impulsePosThisFrame;
                    ImpulseRotLastFrame = impulseRotThisFrame;
                }
            }            
#endif // HAPTICS_CINEMACHINE_SUPPORT
        }
    }

    protected void Update_Internal_Gamepad()
    {
        float lowFrequencyMotor = 0f;
        float highFrequencyMotor = 0f;

        // process each of the effects
        for (int index = 0; index < ActiveEffects.Count; ++index)
        {            
            // does the effect have a duration?
            if (ActiveEffects[index].TimeRemaining > 0)
            {
                // has the efect expired?
                if (ActiveEffects[index].AdvanceTime(Time.deltaTime))
                {
                    // remove the effect
                    ActiveEffects.RemoveAt(index);

                    // update the index and resume
                    --index;
                    continue;
                }
            }

            // apply the effect
            ActiveEffects[index].ApplyTo(ref lowFrequencyMotor, ref highFrequencyMotor);
        }

        if (EnableHaptics)
            Gamepad.current.SetMotorSpeeds(lowFrequencyMotor, highFrequencyMotor);
        else
            Gamepad.current.SetMotorSpeeds(0, 0);
    }

    public void PlayEffectWithIntensity(HapticEffect effect, float intensity = 1f)
    {
        System.Guid playingID;
        PlayEffect(effect, out playingID);

        SetEffectIntensityMultiplier(playingID, intensity);
    }

    public void PlayEffect(HapticEffect effect)
    {
        InternalPlayEffect(effect, effect.Duration, effect.Blending, effect.Looping);
    }

    public void PlayEffect(HapticEffect effect, out System.Guid playingID)
    {
        playingID = InternalPlayEffect(effect, effect.Duration, effect.Blending, effect.Looping);
    }

    public void PlayEffect(HapticEffect effect, float overrideDuration)
    {
        InternalPlayEffect(effect, overrideDuration, effect.Blending, effect.Looping);
    }

    public void PlayEffect(HapticEffect effect, float overrideDuration, out System.Guid playingID)
    {
        playingID = InternalPlayEffect(effect, overrideDuration, effect.Blending, effect.Looping);
    }

    public void PlayEffect(HapticEffect effect, HapticEffect.EBlendMode overrideBlendMode)
    {
        InternalPlayEffect(effect, effect.Duration, overrideBlendMode, effect.Looping);
    }

    public void PlayEffect(HapticEffect effect, HapticEffect.EBlendMode overrideBlendMode, out System.Guid playingID)
    {
        playingID = InternalPlayEffect(effect, effect.Duration, overrideBlendMode, effect.Looping);
    }

    public void PlayEffect(HapticEffect effect, float overrideDuration, HapticEffect.EBlendMode overrideBlendMode)
    {
        InternalPlayEffect(effect, overrideDuration, overrideBlendMode, effect.Looping);
    }    

    public void PlayEffect(HapticEffect effect, float overrideDuration, HapticEffect.EBlendMode overrideBlendMode, out System.Guid playingID)
    {
        playingID = InternalPlayEffect(effect, overrideDuration, overrideBlendMode, effect.Looping);
    }

    protected System.Guid InternalPlayEffect(HapticEffect effect, float duration, HapticEffect.EBlendMode blending, bool looping)
    {
        // validate the effect
        if (!effect.Validate(duration))
        {
            Debug.LogError("Haptic effect " + effect.name + " has an infinite time but is using a curve. Effect will not play.");
            return System.Guid.Empty;
        }

        HapticEffectData effectData = new HapticEffectData(effect, duration, blending, looping);
        ActiveEffects.Add(effectData);

        // sort the effects so that any multiply ones are last
        ActiveEffects.Sort((lhs, rhs) => lhs.Blending.CompareTo(rhs.Blending));

        return effectData.PlayingID;
    }

    public void StopEffect(System.Guid playingID)
    {
        // search for and remove the effect
        for (int index = 0; index < ActiveEffects.Count; ++index)
        {
            if (ActiveEffects[index].PlayingID == playingID)
            {
                ActiveEffects.RemoveAt(index);
                return;
            }
        }
    }

    public void SetEffectDuration(System.Guid playingID, float newDuration)
    {
        // search for and update the duration
        for (int index = 0; index < ActiveEffects.Count; ++index)
        {
            if (ActiveEffects[index].PlayingID == playingID)
            {
                ActiveEffects[index].SetDuration(newDuration);
                return;
            }
        }        
    }

    public void SetEffectIntensityMultiplier(System.Guid playingID, float intensityMultiplier)
    {
        // search for and update the intensity multiplier
        for (int index = 0; index < ActiveEffects.Count; ++index)
        {
            if (ActiveEffects[index].PlayingID == playingID)
            {
                ActiveEffects[index].SetIntensityMultiplier(intensityMultiplier);
                return;
            }
        }
    }

    public void StopAllEffects()
    {
        if (Gamepad.current == null)
            return;

        Gamepad.current.ResetHaptics();
        ActiveEffects.Clear();
    }

    void OnDestroy()
    {
        StopAllEffects();
    }

#region IPausable
    public bool OnPauseRequested()  { return true; }
    public bool OnResumeRequested() { return true; }

    public void OnPause()  
    { 
        if (Gamepad.current == null)
            return;

        Gamepad.current.PauseHaptics();
    }

    public void OnResume() 
    { 
        if (Gamepad.current == null)
            return;

        Gamepad.current.ResumeHaptics();
    }
#endregion
}
