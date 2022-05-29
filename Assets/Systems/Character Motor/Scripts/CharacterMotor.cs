using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody))]
public class CharacterMotor : MonoBehaviour, IDamageable
{
    public class MotorState
    {
        public Rigidbody LinkedRB;
        public GravityTracker LocalGravity;
        public CapsuleCollider LinkedCollider;

        public Vector2 Input_Move;
        public Vector2 Input_Look;
        public bool Input_Jump;
        public bool Input_Run;
        public bool Input_Crouch;
        public bool Input_PrimaryAction;
        public bool Input_SecondaryAction;

        public bool IsGrounded;
        public bool IsRunning;
        public bool IsCrouched;
        public bool IsMovementLocked;
        public bool IsLookingLocked;

        public Transform CurrentParent;
        public SurfaceEffectSource CurrentSurfaceSource;

        public Vector3 UpVector => LocalGravity != null ? LocalGravity.Up : Vector3.up;
        public Vector3 DownVector => LocalGravity != null ? LocalGravity.Down : Vector3.down;
    }
    protected MotorState State = new MotorState();

    [SerializeField] protected CharacterMotorConfig Config;

    [SerializeField] protected UnityEvent<float, float> OnStaminaChanged = new UnityEvent<float, float> ();
    [SerializeField] protected UnityEvent<float, float> OnHealthChanged = new UnityEvent<float, float>();
    [SerializeField] protected UnityEvent<float> OnTookDamage = new UnityEvent<float>();
    [SerializeField] protected UnityEvent<CharacterMotor> OnPlayerDied = new UnityEvent<CharacterMotor>();
    [SerializeField] protected UnityEvent OnPrimary = new UnityEvent();
    [SerializeField] protected UnityEvent OnSecondary = new UnityEvent();

    [Header("Debug Controls")]
    [SerializeField] protected bool DEBUG_OverrideMovement = false;
    [SerializeField] protected Vector2 DEBUG_MovementInput;
    [SerializeField] protected bool DEBUG_ToggleLookLock = false;
    [SerializeField] protected bool DEBUG_ToggleMovementLock = false;

    protected IMovementMode MovementMode;

    protected float PreviousStamina = 0f;
    protected float StaminaRecoveryDelayRemaining = 0f;

    protected float PreviousHealth = 0f;
    protected float HealthRecoveryDelayRemaining = 0f;

    protected float CurrentSurfaceLastTickTime;

    public float CurrentStamina { get; protected set; } = 0f;
    public float CurrentHealth { get; protected set; } = 0f;

    public bool CanCurrentlyJump => Config.CanJump && CurrentStamina >= Config.StaminaCost_Jumping;
    public bool CanCurrentlyRun => Config.CanRun && CurrentStamina > 0f;

    protected virtual void Awake()
    {
        State.LinkedRB = GetComponent<Rigidbody>();
        State.LocalGravity = GetComponent<GravityTracker>();
        State.LinkedCollider = GetComponentInChildren<CapsuleCollider>();

        SwitchMovementMode<MovementMode_Ground>();

        if (MovementMode == null)
        {
            throw new System.NullReferenceException($"There is no IMovementMode attached to {gameObject.name}");
        }

        PreviousStamina = CurrentStamina = Config.MaxStamina;
        PreviousHealth = CurrentHealth = Config.MaxHealth;
    }

    // Start is called before the first frame update
    protected virtual void Start()
    {
        OnStaminaChanged.Invoke(CurrentStamina, Config.MaxStamina);
        OnHealthChanged.Invoke(CurrentHealth, Config.MaxHealth);
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        if (DEBUG_ToggleLookLock)
        {
            DEBUG_ToggleLookLock = false;
            State.IsLookingLocked = !State.IsLookingLocked;
        }
        if (DEBUG_ToggleMovementLock)
        {
            DEBUG_ToggleMovementLock = false;
            State.IsMovementLocked = !State.IsMovementLocked;
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
        if (DEBUG_OverrideMovement)
            State.Input_Move = DEBUG_MovementInput;

        // movement locked?
        if (State.IsMovementLocked)
            State.Input_Move = Vector2.zero;

        MovementMode.FixedUpdate_PreGroundedCheck();

        bool wasGrounded = State.IsGrounded;

        RaycastHit groundCheckResult = MovementMode.FixedUpdate_GroundedCheck();

        if (State.IsGrounded)
        {
            // check for a surface effect
            SurfaceEffectSource surfaceEffectSource = null;
            if (groundCheckResult.collider.gameObject.TryGetComponent<SurfaceEffectSource>(out surfaceEffectSource))
                SetSurfaceEffectSource(surfaceEffectSource);
            else
                SetSurfaceEffectSource(null);

            UpdateSurfaceEffects();

            // have we returned to the ground
            if (!wasGrounded)
                MovementMode.FixedUpdate_OnBecameGrounded();
        }
        else
            SetSurfaceEffectSource(null);

        MovementMode.FixedUpdate_TickMovement(groundCheckResult);
    }

    protected virtual void LateUpdate()
    {
        MovementMode.LateUpdate_Tick();
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
        if (State.IsRunning && State.IsGrounded)
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

    public void ConsumeStamina(float amount)
    {
        CurrentStamina = Mathf.Max(CurrentStamina - amount, 0f);
        StaminaRecoveryDelayRemaining = Config.StaminaRecoveryDelay;
    }

    public void SetMovementLock(bool locked)
    {
        State.IsMovementLocked = locked;
    }

    public void SetLookLock(bool locked)
    {
        State.IsLookingLocked = locked;
    }

    void UpdateSurfaceEffects()
    {
        // no surface effect
        if (State.CurrentSurfaceSource == null)
            return;

        // time to expire the surface effect?
        if (CurrentSurfaceLastTickTime + State.CurrentSurfaceSource.PersistenceTime < Time.time)
        {
            State.CurrentSurfaceSource = null;
            return;
        }
    }

    void SetSurfaceEffectSource(SurfaceEffectSource newSource)
    {
        // changing to a new effect?
        if (newSource != null && newSource != State.CurrentSurfaceSource)
        {
            State.CurrentSurfaceSource = newSource;
            CurrentSurfaceLastTickTime = Time.time;
        } // on same source?
        else if (newSource != null && newSource == State.CurrentSurfaceSource)
        {
            CurrentSurfaceLastTickTime = Time.time;
        }
    }

    public void SwitchMovementMode<T>() where T : IMovementMode
    {
        MovementMode = GetComponent<T>();

        MovementMode.Initialise(Config, this, State);
    }
}
