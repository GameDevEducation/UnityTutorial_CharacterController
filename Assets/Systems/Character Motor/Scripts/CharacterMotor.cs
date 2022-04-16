using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(GravityTracker))]
public class CharacterMotor : MonoBehaviour, IDamageable
{
    [SerializeField] protected CharacterMotorConfig Config;

    [SerializeField] protected UnityEvent<bool> OnRunChanged = new UnityEvent<bool> ();
    [SerializeField] protected UnityEvent<Vector3> OnHitGround = new UnityEvent<Vector3>();
    [SerializeField] protected UnityEvent<Vector3> OnBeginJump = new UnityEvent<Vector3>();
    [SerializeField] protected UnityEvent<Vector3, float> OnFootstep = new UnityEvent<Vector3, float>();

    [SerializeField] protected UnityEvent<float, float> OnStaminaChanged = new UnityEvent<float, float> ();
    [SerializeField] protected UnityEvent<float, float> OnHealthChanged = new UnityEvent<float, float>();
    [SerializeField] protected UnityEvent<float> OnTookDamage = new UnityEvent<float>();
    [SerializeField] protected UnityEvent<CharacterMotor> OnPlayerDied = new UnityEvent<CharacterMotor>();

    [Header("Debug Controls")]
    [SerializeField] protected bool DEBUG_OverrideMovement = false;
    [SerializeField] protected Vector2 DEBUG_MovementInput;
    [SerializeField] protected bool DEBUG_ToggleLookLock = false;
    [SerializeField] protected bool DEBUG_ToggleMovementLock = false;

    protected Rigidbody LinkedRB;
    protected GravityTracker LocalGravity;
    protected CapsuleCollider LinkedCollider;

    protected float JumpTimeRemaining = 0f;
    protected float TimeSinceLastFootstepAudio = 0f;
    protected float TimeInAir = 0f;
    protected float OriginalDrag;
    protected float Camera_CurrentTime = 0f;

    protected float PreviousStamina = 0f;
    protected float StaminaRecoveryDelayRemaining = 0f;

    protected float PreviousHealth = 0f;
    protected float HealthRecoveryDelayRemaining = 0f;

    public SurfaceEffectSource CurrentSurfaceSource { get; protected set; } = null;
    protected float CurrentSurfaceLastTickTime;

    public bool IsMovementLocked { get; protected set; } = false;
    public bool IsLookingLocked { get; protected set; } = false;
    public bool IsJumping => IsInJumpingRisePhase || IsInJumpingFallPhase;
    public bool IsInJumpingFallPhase { get; protected set; } = false;
    public bool IsInJumpingRisePhase { get; protected set; } = false;
    public int JumpCount { get; protected set; } = 0;
    public bool IsRunning { get; protected set; } = false;
    public bool IsGrounded { get; protected set; } = true;
    public bool InCoyoteTime => CoyoteTimeRemaining > 0f;
    public bool IsGroundedOrInCoyoteTime => IsGrounded || InCoyoteTime;
    public bool IsCrouched { get; protected set; } = false;
    public bool InCrouchTransition { get; protected set; } = false;
    public bool TargetCrouchState { get; protected set; } = false;
    public float CrouchTransitionProgress { get; protected set; } = 1f;
    public float CoyoteTimeRemaining { get; protected set; } = 0f;
    public float CurrentStamina { get; protected set; } = 0f;
    public float CurrentHealth { get; protected set; } = 0f;
    public bool CanCurrentlyJump => Config.CanJump && CurrentStamina >= Config.StaminaCost_Jumping;
    public bool CanCurrentlyRun => Config.CanRun && CurrentStamina > 0f;

    public float CurrentHeight
    {
        get
        {
            if (InCrouchTransition)
                return Mathf.Lerp(Config.CrouchHeight, Config.Height, CrouchTransitionProgress);

            return IsCrouched ? Config.CrouchHeight : Config.Height;
        }
    }

    public float CurrentMaxSpeed
    {
        get
        {
            float speed = 0f;

            if (IsGroundedOrInCoyoteTime || IsJumping)
                speed = (IsRunning ? Config.RunSpeed : Config.WalkSpeed) * (IsCrouched ? Config.CrouchSpeedMultiplier : 1f);
            else
                speed = Config.CanAirControl ? Config.AirControlMaxSpeed : 0f;

            return CurrentSurfaceSource != null ? CurrentSurfaceSource.Effect(speed, EEffectableParameter.Speed) : speed;
        }
    }

    protected Vector2 _Input_Move;
    protected Vector2 _Input_Look;
    protected bool _Input_Jump;
    protected bool _Input_Run;
    protected bool _Input_Crouch;
    protected bool _Input_PrimaryAction;
    protected bool _Input_SecondaryAction;

    protected virtual void Awake()
    {
        LinkedRB = GetComponent<Rigidbody>();
        LocalGravity = GetComponent<GravityTracker>();
        LinkedCollider = GetComponentInChildren<CapsuleCollider>();

        PreviousStamina = CurrentStamina = Config.MaxStamina;
        PreviousHealth = CurrentHealth = Config.MaxHealth;
    }

    // Start is called before the first frame update
    protected virtual void Start()
    {
        LinkedCollider.material = Config.Material_Default;
        LinkedCollider.radius = Config.Radius;
        LinkedCollider.height = CurrentHeight;
        LinkedCollider.center = Vector3.up * (CurrentHeight * 0.5f);

        OriginalDrag = LinkedRB.drag;

        OnStaminaChanged.Invoke(CurrentStamina, Config.MaxStamina);
        OnHealthChanged.Invoke(CurrentHealth, Config.MaxHealth);
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        if (DEBUG_ToggleLookLock)
        {
            DEBUG_ToggleLookLock = false;
            IsLookingLocked = !IsLookingLocked;
        }
        if (DEBUG_ToggleMovementLock)
        {
            DEBUG_ToggleMovementLock = false;
            IsMovementLocked = !IsMovementLocked;
        }

        UpdateHealth();
        UpdateStamina();

        if (PreviousStamina != CurrentStamina)
        {
            PreviousStamina = CurrentStamina;
            OnStaminaChanged.Invoke(CurrentStamina, Config.MaxStamina);
        }

        if (PreviousHealth != CurrentHealth)
        {
            PreviousHealth = CurrentHealth;
            OnHealthChanged?.Invoke(CurrentHealth, Config.MaxHealth);
        }
    }

    protected void FixedUpdate()
    {
        bool wasGrounded = IsGrounded;
        bool wasRunning = IsRunning;

        // align to the local gravity vector
        transform.rotation = Quaternion.FromToRotation(transform.up, LocalGravity.Up) * transform.rotation;

        RaycastHit groundCheckResult = UpdateIsGrounded();

        UpdateSurfaceEffects();

        // activate coyote time?
        if (wasGrounded && !IsGrounded)
            CoyoteTimeRemaining = Config.CoyoteTime;
        else
        {
            // reduce the coyote time
            if (CoyoteTimeRemaining > 0)
                CoyoteTimeRemaining -= Time.deltaTime;
        }

        UpdateRunning(groundCheckResult);

        if (wasRunning != IsRunning)
            OnRunChanged.Invoke(IsRunning);

        // switch back to grounded material
        if (wasGrounded != IsGrounded && IsGrounded)
        {
            LinkedCollider.material = Config.Material_Default;
            LinkedRB.drag = OriginalDrag;
            TimeSinceLastFootstepAudio = 0f;
            CoyoteTimeRemaining = 0f;

            if (TimeInAir >= Config.MinAirTimeForLandedSound)
                OnHitGround.Invoke(LinkedRB.position);
        }

        // track how long we have been in the air
        TimeInAir = IsGroundedOrInCoyoteTime ? 0f : (TimeInAir + Time.deltaTime);

        UpdateMovement(groundCheckResult);
    }

    protected virtual void LateUpdate()
    {
        UpdateCrouch();
    }

    public Transform CurrentParent { get; protected set; } = null;

    protected RaycastHit UpdateIsGrounded()
    {
        RaycastHit hitResult;

        // currently performing a jump
        if (JumpTimeRemaining > 0)
        {
            IsGrounded = false;
            return new RaycastHit();
        }

        Vector3 startPos = LinkedRB.position + LocalGravity.Up * CurrentHeight * 0.5f;
        float groundCheckDistance = (CurrentHeight * 0.5f) + Config.GroundedCheckBuffer;

        // perform our spherecast
        if (Physics.Raycast(startPos, LocalGravity.Down, out hitResult, groundCheckDistance,
                            Config.GroundedLayerMask, QueryTriggerInteraction.Ignore))
        {
            IsGrounded = true;
            JumpCount = 0;
            JumpTimeRemaining = 0f;
            IsInJumpingFallPhase = false;

            // check for a surface effect
            SurfaceEffectSource surfaceEffectSource = null;
            if (hitResult.collider.gameObject.TryGetComponent<SurfaceEffectSource>(out surfaceEffectSource))
                SetSurfaceEffectSource(surfaceEffectSource);
            else
                SetSurfaceEffectSource(null);

            // is autoparenting enabled?
            if (Config.AutoParent)
            {
                // auto parent to anything!
                if (Config.AutoParentMode == CharacterMotorConfig.EAutoParentMode.Anything)
                {
                    if (hitResult.transform != CurrentParent)
                    {
                        CurrentParent = hitResult.transform;
                        transform.SetParent(CurrentParent, true);
                    }
                }
                else
                {
                    // search for our autoparent script
                    var target = hitResult.transform.gameObject.GetComponentInParent<CharacterMotorAutoParentTarget>();
                    if (target != null && target.transform != CurrentParent)
                    {
                        CurrentParent = target.transform;
                        transform.SetParent(CurrentParent, true);
                    }
                }
            }
        }
        else
        {
            SetSurfaceEffectSource(null);
            IsGrounded = false;
        }

        return hitResult;
    }

    protected void UpdateMovement(RaycastHit groundCheckResult)
    {
        if (DEBUG_OverrideMovement)
            _Input_Move = DEBUG_MovementInput;

        // movement locked?
        if (IsMovementLocked)
            _Input_Move = Vector2.zero;

        // calculate our movement input
        Vector3 movementVector = transform.forward * _Input_Move.y + transform.right * _Input_Move.x;
        movementVector *= CurrentMaxSpeed;

        // are we on the ground?
        if (IsGroundedOrInCoyoteTime)
        {
            // project onto the current surface
            movementVector = Vector3.ProjectOnPlane(movementVector, groundCheckResult.normal);

            // trying to move up too steep a slope
            if (movementVector.y > 0 && Vector3.Angle(LocalGravity.Up, groundCheckResult.normal) > Config.SlopeLimit)
                movementVector = Vector3.zero;
        } // in the air
        else
        {
            movementVector += LocalGravity.Down * Config.FallVelocity;
        }

        UpdateJumping(ref movementVector);

        if (IsGroundedOrInCoyoteTime && !IsJumping)
        {
            CheckForStepUp(ref movementVector);

            UpdateFootstepAudio();
        }

        // update the velocity
        LinkedRB.velocity = Vector3.MoveTowards(LinkedRB.velocity, movementVector, Config.Acceleration);
    }

    public void OnPerformHeal(GameObject source, float amount)
    {
        CurrentHealth = Mathf.Min(CurrentHealth + amount, Config.MaxHealth);
    }

    public void OnTakeDamage(GameObject source, float amount)
    {
        OnTookDamage.Invoke(amount);

        CurrentHealth = Mathf.Max(CurrentHealth - amount, 0f);
        HealthRecoveryDelayRemaining = Config.HealthRecoveryDelay;

        // have we died?
        if (CurrentHealth <= 0f && PreviousHealth > 0f)
            OnPlayerDied.Invoke(this);
    }

    protected void UpdateHealth()
    {
        // do we have health to recover?
        if (CurrentHealth < Config.MaxHealth)
        {
            if (HealthRecoveryDelayRemaining > 0f)
                HealthRecoveryDelayRemaining -= Time.deltaTime;

            if (HealthRecoveryDelayRemaining <= 0f)
                CurrentHealth = Mathf.Min(CurrentHealth + Config.HealthRecoveryRate * Time.deltaTime,
                                          Config.MaxHealth);
        }
    }

    protected void UpdateStamina()
    {
        // if we're running consume stamina
        if (IsRunning && IsGrounded)
            ConsumeStamina(Config.StaminaCost_Running * Time.deltaTime);
        else if (CurrentStamina < Config.MaxStamina) // if we're able to recover
        {
            if (StaminaRecoveryDelayRemaining > 0f)
                StaminaRecoveryDelayRemaining -= Time.deltaTime;

            if (StaminaRecoveryDelayRemaining <= 0f)
                CurrentStamina = Mathf.Min(CurrentStamina + Config.StaminaRecoveryRate * Time.deltaTime,
                                           Config.MaxStamina);
        }
    }

    protected void ConsumeStamina(float amount)
    {
        CurrentStamina = Mathf.Max(CurrentStamina - amount, 0f);
        StaminaRecoveryDelayRemaining = Config.StaminaRecoveryDelay;
    }

    protected void UpdateFootstepAudio()
    {
        // is the player attempting to move?
        if (_Input_Move.magnitude > float.Epsilon)
        {
            // update time since last audio
            TimeSinceLastFootstepAudio += Time.deltaTime;

            // time for footstep audio?
            float footstepInterval = IsRunning ? Config.FootstepInterval_Running : Config.FootstepInterval_Walking;
            if (TimeSinceLastFootstepAudio >= footstepInterval)
            {
                OnFootstep.Invoke(LinkedRB.position, LinkedRB.velocity.magnitude);

                TimeSinceLastFootstepAudio -= footstepInterval;
            }
        }
    }

    protected void CheckForStepUp(ref Vector3 movementVector)
    {
        Vector3 lookAheadStartPoint = LinkedRB.position + LocalGravity.Up * (Config.StepCheck_MaxStepHeight * 0.5f);
        Vector3 lookAheadDirection = movementVector.normalized;
        float lookAheadDistance = Config.Radius + Config.StepCheck_LookAheadRange;

        // check if there is a potential step ahead
        if (Physics.Raycast(lookAheadStartPoint, lookAheadDirection, lookAheadDistance, 
                            Config.GroundedLayerMask, QueryTriggerInteraction.Ignore))
        {
            lookAheadStartPoint = LinkedRB.position + LocalGravity.Up * Config.StepCheck_MaxStepHeight;

            // check if there is clear space above the step
            if (!Physics.Raycast(lookAheadStartPoint, lookAheadDirection, lookAheadDistance,
                                Config.GroundedLayerMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 candidatePoint = lookAheadStartPoint + lookAheadDirection * lookAheadDistance;

                // check the surface of the step
                RaycastHit hitResult;
                if (Physics.Raycast(candidatePoint, LocalGravity.Down, out hitResult, Config.StepCheck_MaxStepHeight * 2f,
                                    Config.GroundedLayerMask, QueryTriggerInteraction.Ignore))
                {
                    // is the step shallow enough in slope
                    if (Vector3.Angle(LocalGravity.Up, hitResult.normal) <= Config.SlopeLimit)
                    {
                        LinkedRB.position = hitResult.point;
                    }
                }
            }
        }
    }

    protected void UpdateJumping(ref Vector3 movementVector)
    {
        // jump requested?
        bool triggeredJumpThisFrame = false;
        if (_Input_Jump && CanCurrentlyJump)
        {
            _Input_Jump = false;

            // check if we can jump
            bool triggerJump = true;
            int numJumpsPermitted = Config.CanDoubleJump ? 2 : 1;
            if (JumpCount >= numJumpsPermitted)
                triggerJump = false;
            if (!IsGroundedOrInCoyoteTime && !IsJumping)
                triggerJump = false;

            // jump is permitted?
            if (triggerJump)
            {
                if (JumpCount == 0)
                    triggeredJumpThisFrame = true;

                float jumpTime = Config.JumpTime;
                if (CurrentSurfaceSource != null)
                    jumpTime = CurrentSurfaceSource.Effect(jumpTime, EEffectableParameter.JumpTime);

                LinkedCollider.material = Config.Material_Jumping;
                LinkedRB.drag = 0;
                JumpTimeRemaining += jumpTime;
                IsInJumpingRisePhase = true;
                IsInJumpingFallPhase = false;
                CoyoteTimeRemaining = 0f;
                ++JumpCount;

                OnBeginJump.Invoke(LinkedRB.position);

                ConsumeStamina(Config.StaminaCost_Jumping);
            }
        }

        if (IsInJumpingRisePhase)
        {
            // update remaining jump time if not jumping this frame
            if (!triggeredJumpThisFrame)
                JumpTimeRemaining -= Time.deltaTime;

            // jumping finished
            if (JumpTimeRemaining <= 0)
            {
                IsInJumpingRisePhase = false;
                IsInJumpingFallPhase = true;
            }
            else
            {
                Vector3 startPos = LinkedRB.position + LocalGravity.Up * CurrentHeight * 0.5f;
                float ceilingCheckRadius = Config.Radius + Config.CeilingCheckRadiusBuffer;
                float ceilingCheckDistance = (CurrentHeight * 0.5f) - Config.Radius + Config.GroundedCheckBuffer;

                // perform our spherecast
                RaycastHit ceilingHitResult;
                if (Physics.SphereCast(startPos, ceilingCheckRadius, LocalGravity.Up, out ceilingHitResult,
                                       ceilingCheckDistance, Config.GroundedLayerMask, QueryTriggerInteraction.Ignore))
                {
                    IsInJumpingRisePhase = false;
                    IsInJumpingFallPhase = true;
                    JumpTimeRemaining = 0f;
                    movementVector.y = 0f;
                }
                else
                {
                    float jumpVelocity = Config.JumpVelocity;

                    if (CurrentSurfaceSource != null)
                        jumpVelocity = CurrentSurfaceSource.Effect(jumpVelocity, EEffectableParameter.JumpVelocity);

                    
                    movementVector += LocalGravity.Up * (jumpVelocity + Vector3.Dot(movementVector, LocalGravity.Down));
                }
            }
        }
    }

    protected void UpdateRunning(RaycastHit groundCheckResult)
    {
        // no longer able to run?
        if (!CanCurrentlyRun)
        {
            IsRunning = false;
            return;
        }

        // stop running if no input
        if (_Input_Move.magnitude < float.Epsilon)
            IsRunning = false;

        // not grounded AND not jumping
        if (!IsGroundedOrInCoyoteTime && !IsJumping)
        {
            IsRunning = false;
            return;
        }

        // cannot run?
        if (!Config.CanRun)
        {
            IsRunning = false;
            return;
        }

        // setup our run toggle
        if (Config.IsRunToggle)
        {
            if (_Input_Run && !IsRunning)
                IsRunning = true;
        }
        else
            IsRunning = _Input_Run;
    }

    protected void UpdateCrouch()
    {
        // do nothing if either movement or looking are locked
        if (IsMovementLocked || IsLookingLocked)
            return;

        // not allowed to crouch?
        if (!Config.CanCrouch)
            return;

        // are we jumping or in the air due to falling etc
        if (IsJumping || !IsGroundedOrInCoyoteTime)
        {
            // crouched or transitioning to crouched
            if (IsCrouched || TargetCrouchState)
            {
                TargetCrouchState = false;
                InCrouchTransition = true;
            }
        }
        else if (Config.IsCrouchToggle)
        {
            // toggle crouch state?
            if (_Input_Crouch)
            {
                _Input_Crouch = false;

                TargetCrouchState = !TargetCrouchState;
                InCrouchTransition = true;
            }
        }
        else
        {
            // request crouch state different to current target
            if (_Input_Crouch != TargetCrouchState)
            {
                TargetCrouchState = _Input_Crouch;
                InCrouchTransition = true;
            }
        }

        // update crouch if mid transition
        if (InCrouchTransition)
        {
            // Update the progress
            CrouchTransitionProgress = Mathf.MoveTowards(CrouchTransitionProgress,
                                                         TargetCrouchState ? 0f : 1f,
                                                         Time.deltaTime / Config.CrouchTransitionTime);

            // update the collider and camera
            LinkedCollider.height = CurrentHeight;
            LinkedCollider.center = Vector3.up * (CurrentHeight * 0.5f);

            // finished changing crouch state
            if (Mathf.Approximately(CrouchTransitionProgress, TargetCrouchState ? 0f : 1f))
            {
                IsCrouched = TargetCrouchState;
                InCrouchTransition = false;
            }
        }
    }


    public void SetMovementLock(bool locked)
    {
        IsMovementLocked = locked;
    }

    public void SetLookLock(bool locked)
    {
        IsLookingLocked = locked;
    }

    void UpdateSurfaceEffects()
    {
        // no surface effect
        if (CurrentSurfaceSource == null)
            return;

        // time to expire the surface effect?
        if (CurrentSurfaceLastTickTime + CurrentSurfaceSource.PersistenceTime < Time.time)
        {
            CurrentSurfaceSource = null;
            return;
        }
    }

    void SetSurfaceEffectSource(SurfaceEffectSource newSource)
    {
        // changing to a new effect?
        if (newSource != null && newSource != CurrentSurfaceSource)
        {
            CurrentSurfaceSource = newSource;
            CurrentSurfaceLastTickTime = Time.time;
        } // on same source?
        else if (newSource != null && newSource == CurrentSurfaceSource)
        {
            CurrentSurfaceLastTickTime = Time.time;
        }
    }
}
