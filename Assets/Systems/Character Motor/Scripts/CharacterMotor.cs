using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody))]
public class CharacterMotor : MonoBehaviour
{
    [SerializeField] protected CharacterMotorConfig Config;
    [SerializeField] protected Transform LinkedCamera;

    [SerializeField] protected UnityEvent<bool> OnRunChanged = new UnityEvent<bool> ();
    [SerializeField] protected UnityEvent<Vector3> OnHitGround = new UnityEvent<Vector3>();
    [SerializeField] protected UnityEvent<Vector3> OnBeginJump = new UnityEvent<Vector3>();
    [SerializeField] protected UnityEvent<Vector3, float> OnFootstep = new UnityEvent<Vector3, float>();

    [Header("Debug Controls")]
    [SerializeField] protected bool DEBUG_OverrideMovement = false;
    [SerializeField] protected Vector2 DEBUG_MovementInput;
    [SerializeField] protected bool DEBUG_ToggleLookLock = false;
    [SerializeField] protected bool DEBUG_ToggleMovementLock = false;

    protected Rigidbody LinkedRB;
    protected CapsuleCollider LinkedCollider;
    protected float CurrentCameraPitch = 0f;

    protected float JumpTimeRemaining = 0f;
    protected float TimeSinceLastFootstepAudio = 0f;
    protected float TimeInAir = 0f;
    protected float OriginalDrag;
    protected float Camera_CurrentTime = 0f;

    public bool IsMovementLocked { get; protected set; } = false;
    public bool IsLookingLocked { get; protected set; } = false;
    public bool IsJumping { get; protected set; } = false;
    public int JumpCount { get; protected set; } = 0;
    public bool IsRunning { get; protected set; } = false;
    public bool IsGrounded { get; protected set; } = true;
    public bool InCoyoteTime => CoyoteTimeRemaining > 0f;
    public bool IsGroundedOrInCoyoteTime => IsGrounded || InCoyoteTime;
    public bool IsCrouched { get; protected set; } = false;
    public bool SendUIInteractions { get; protected set; } = true;
    public bool InCrouchTransition { get; protected set; } = false;
    public bool TargetCrouchState { get; protected set; } = false;
    public float CrouchTransitionProgress { get; protected set; } = 1f;
    public float CoyoteTimeRemaining { get; protected set; } = 0f;

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
            if (IsGroundedOrInCoyoteTime)
                return (IsRunning ? Config.RunSpeed : Config.WalkSpeed) * (IsCrouched ? Config.CrouchSpeedMultiplier : 1f);

            return Config.CanAirControl ? Config.AirControlMaxSpeed : 0f;
        }
    }


    #region Input System Handling
    protected Vector2 _Input_Move;
    protected void OnMove(InputValue value)
    {
        _Input_Move = value.Get<Vector2>();
    }

    protected Vector2 _Input_Look;
    protected void OnLook(InputValue value)
    {
        _Input_Look = value.Get<Vector2>();
    }

    protected bool _Input_Jump;
    protected void OnJump(InputValue value)
    {
        _Input_Jump = value.isPressed;
    }

    protected bool _Input_Run;
    protected void OnRun(InputValue value)
    {
        _Input_Run = value.isPressed;
    }

    protected bool _Input_Crouch;
    protected void OnCrouch(InputValue value)
    {
        _Input_Crouch = value.isPressed;
    }    

    protected bool _Input_PrimaryAction;
    protected void OnPrimaryAction(InputValue value)
    {
        _Input_PrimaryAction = value.isPressed;

        // need to inject pointer event
        if (_Input_PrimaryAction && SendUIInteractions)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = Mouse.current.position.ReadValue();

            // raycast against the UI
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach(RaycastResult result in results)
            {
                if (result.distance < Config.MaxInteractionDistance)
                    ExecuteEvents.Execute(result.gameObject, pointerData, ExecuteEvents.pointerClickHandler);
            }
        }
    }

    protected bool _Input_SecondaryAction;
    protected void OnSecondaryAction(InputValue value)
    {
        _Input_SecondaryAction = value.isPressed;
    }

    #endregion

    private void Awake()
    {
        LinkedRB = GetComponent<Rigidbody>();
        LinkedCollider = GetComponent<CapsuleCollider>();
        SendUIInteractions = Config.SendUIInteractions;
    }

    // Start is called before the first frame update
    void Start()
    {
        SetCursorLock(true);

        LinkedCollider.material = Config.Material_Default;
        LinkedCollider.radius = Config.Radius;
        LinkedCollider.height = CurrentHeight;
        LinkedCollider.center = Vector3.up * (CurrentHeight * 0.5f);

        LinkedCamera.transform.localPosition = Vector3.up * (CurrentHeight + Config.Camera_VerticalOffset);

        OriginalDrag = LinkedRB.drag;
    }

    // Update is called once per frame
    void Update()
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
    }

    protected void FixedUpdate()
    {
        bool wasGrounded = IsGrounded;
        bool wasRunning = IsRunning;

        RaycastHit groundCheckResult = UpdateIsGrounded();

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

    protected void LateUpdate()
    {
        UpdateCamera();

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

        Vector3 startPos = LinkedRB.position + Vector3.up * CurrentHeight * 0.5f;
        float groundCheckDistance = (CurrentHeight * 0.5f) + Config.GroundedCheckBuffer;

        // perform our spherecast
        if (Physics.Raycast(startPos, Vector3.down, out hitResult, groundCheckDistance,
                            Config.GroundedLayerMask, QueryTriggerInteraction.Ignore))
        {
            IsGrounded = true;
            JumpCount = 0;
            JumpTimeRemaining = 0f;

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
            IsGrounded = false;

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
            if (movementVector.y > 0 && Vector3.Angle(Vector3.up, groundCheckResult.normal) > Config.SlopeLimit)
                movementVector = Vector3.zero;
        } // in the air
        else
        {
            movementVector += Vector3.down * Config.FallVelocity;
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
        Vector3 lookAheadStartPoint = LinkedRB.position + Vector3.up * (Config.StepCheck_MaxStepHeight * 0.5f);
        Vector3 lookAheadDirection = movementVector.normalized;
        float lookAheadDistance = Config.Radius + Config.StepCheck_LookAheadRange;

        // check if there is a potential step ahead
        if (Physics.Raycast(lookAheadStartPoint, lookAheadDirection, lookAheadDistance, 
                            Config.GroundedLayerMask, QueryTriggerInteraction.Ignore))
        {
            lookAheadStartPoint = LinkedRB.position + Vector3.up * Config.StepCheck_MaxStepHeight;

            // check if there is clear space above the step
            if (!Physics.Raycast(lookAheadStartPoint, lookAheadDirection, lookAheadDistance,
                                Config.GroundedLayerMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 candidatePoint = lookAheadStartPoint + lookAheadDirection * lookAheadDistance;

                // check the surface of the step
                RaycastHit hitResult;
                if (Physics.Raycast(candidatePoint, Vector3.down, out hitResult, Config.StepCheck_MaxStepHeight * 2f,
                                    Config.GroundedLayerMask, QueryTriggerInteraction.Ignore))
                {
                    // is the step shallow enough in slope
                    if (Vector3.Angle(Vector3.up, hitResult.normal) <= Config.SlopeLimit)
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
        if (_Input_Jump)
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

                LinkedCollider.material = Config.Material_Jumping;
                LinkedRB.drag = 0;
                JumpTimeRemaining += Config.JumpTime;
                IsJumping = true;
                CoyoteTimeRemaining = 0f;
                ++JumpCount;

                OnBeginJump.Invoke(LinkedRB.position);
            }
        }

        if (IsJumping)
        {
            // update remaining jump time if not jumping this frame
            if (!triggeredJumpThisFrame)
                JumpTimeRemaining -= Time.deltaTime;

            // jumping finished
            if (JumpTimeRemaining <= 0)
                IsJumping = false;
            else
            {
                Vector3 startPos = LinkedRB.position + Vector3.up * CurrentHeight * 0.5f;
                float ceilingCheckRadius = Config.Radius + Config.CeilingCheckRadiusBuffer;
                float ceilingCheckDistance = (CurrentHeight * 0.5f) - Config.Radius + Config.GroundedCheckBuffer;

                // perform our spherecast
                RaycastHit ceilingHitResult;
                if (Physics.SphereCast(startPos, ceilingCheckRadius, Vector3.up, out ceilingHitResult,
                                       ceilingCheckDistance, Config.GroundedLayerMask, QueryTriggerInteraction.Ignore))
                {
                    IsJumping = false;
                    JumpTimeRemaining = 0f;
                    movementVector.y = 0f;
                }
                else
                {
                    movementVector.y = Config.JumpVelocity;
                }
            }
        }
    }

    protected void UpdateRunning(RaycastHit groundCheckResult)
    {
        // stop running if no input
        if (_Input_Move.magnitude < float.Epsilon)
            IsRunning = false;

        // not grounded
        if (!IsGroundedOrInCoyoteTime)
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

    protected void UpdateCamera()
    {
        // not allowed to look around?
        if (IsLookingLocked)
            return;

        // ignore any camera input for a brief time (mostly helps editor side when hitting play button)
        if (Camera_CurrentTime < Config.Camera_InitialDiscardTime)
        {
            Camera_CurrentTime += Time.deltaTime;
            return;
        }

        // calculate our camera inputs
        float cameraYawDelta = _Input_Look.x * Config.Camera_HorizontalSensitivity * Time.deltaTime;
        float cameraPitchDelta = _Input_Look.y * Config.Camera_VerticalSensitivity * Time.deltaTime *
                                 (Config.Camera_InvertY ? 1f : -1f);

        // rotate the character
        transform.localRotation = transform.localRotation * Quaternion.Euler(0f, cameraYawDelta, 0f);

        // tilt the camera
        CurrentCameraPitch = Mathf.Clamp(CurrentCameraPitch + cameraPitchDelta,
                                         Config.Camera_MinPitch,
                                         Config.Camera_MaxPitch);
        LinkedCamera.transform.localRotation = Quaternion.Euler(CurrentCameraPitch, 0f, 0f);
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
            LinkedCamera.transform.localPosition = Vector3.up * (CurrentHeight + Config.Camera_VerticalOffset);

            // finished changing crouch state
            if (Mathf.Approximately(CrouchTransitionProgress, TargetCrouchState ? 0f : 1f))
            {
                IsCrouched = TargetCrouchState;
                InCrouchTransition = false;
            }
        }
    }

    public void SetCursorLock(bool locked)
    {
        Cursor.visible = !locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
    }

    public void SetMovementLock(bool locked)
    {
        IsMovementLocked = locked;
    }

    public void SetLookLock(bool locked)
    {
        IsLookingLocked = locked;
    }
}
