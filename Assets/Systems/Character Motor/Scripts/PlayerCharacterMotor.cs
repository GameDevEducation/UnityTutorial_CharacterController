using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Rigidbody))]
public class PlayerCharacterMotor : CharacterMotor
{
    [SerializeField] protected Transform LinkedCamera;

    protected float CurrentCameraPitch = 0f;
    protected float HeadbobProgress = 0f;

    public bool SendUIInteractions { get; protected set; } = true;

    #region Input System Handling
    protected void OnMove(InputValue value)
    {
        _Input_Move = value.Get<Vector2>();
    }

    protected void OnLook(InputValue value)
    {
        _Input_Look = value.Get<Vector2>();
    }

    protected void OnJump(InputValue value)
    {
        _Input_Jump = value.isPressed;
    }

    protected void OnRun(InputValue value)
    {
        _Input_Run = value.isPressed;
    }

    protected void OnCrouch(InputValue value)
    {
        _Input_Crouch = value.isPressed;
    }

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

            foreach (RaycastResult result in results)
            {
                if (result.distance < Config.MaxInteractionDistance)
                    ExecuteEvents.Execute(result.gameObject, pointerData, ExecuteEvents.pointerClickHandler);
            }
        }
    }

    protected void OnSecondaryAction(InputValue value)
    {
        _Input_SecondaryAction = value.isPressed;
    }

    #endregion

    protected override void Awake()
    {
        base.Awake();

        SendUIInteractions = Config.SendUIInteractions;
    }

    protected override void Start()
    {
        SetCursorLock(true);

        base.Start();

        LinkedCamera.transform.localPosition = Vector3.up * (CurrentHeight + Config.Camera_VerticalOffset);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();

        UpdateCamera();
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

        // allow surface to effect sensitivity
        float hSensitivity = Config.Camera_HorizontalSensitivity;
        float vSensitivity = Config.Camera_VerticalSensitivity;
        if (CurrentSurfaceSource != null)
        {
            hSensitivity = CurrentSurfaceSource.Effect(hSensitivity, EEffectableParameter.CameraSensitivity);
            vSensitivity = CurrentSurfaceSource.Effect(vSensitivity, EEffectableParameter.CameraSensitivity);
        }

        // calculate our camera inputs
        float cameraYawDelta = _Input_Look.x * hSensitivity * Time.deltaTime;
        float cameraPitchDelta = _Input_Look.y * vSensitivity * Time.deltaTime * (Config.Camera_InvertY ? 1f : -1f);

        // rotate the character
        transform.localRotation = transform.localRotation * Quaternion.Euler(0f, cameraYawDelta, 0f);

        LinkedCamera.transform.localPosition = Vector3.up * (CurrentHeight + Config.Camera_VerticalOffset);

        // headbob enabled and on the ground?
        if (Config.Headbob_Enable && IsGrounded)
        {
            float currentSpeed = LinkedRB.velocity.magnitude;

            // moving fast enough to bob?
            Vector3 defaultCameraOffset = Vector3.up * (CurrentHeight + Config.Camera_VerticalOffset);
            if (currentSpeed >= Config.Headbob_MinSpeedToBob)
            {
                float speedFactor = currentSpeed / (Config.CanRun ? Config.RunSpeed : Config.WalkSpeed);

                // update our progress
                HeadbobProgress += Time.deltaTime / Config.Headbob_PeriodVsSpeedFactor.Evaluate(speedFactor);
                HeadbobProgress %= 1f;

                // determine the maximum translations
                float maxVTranslation = Config.Headbob_VTranslationVsSpeedFactor.Evaluate(speedFactor);
                float maxHTranslation = Config.Headbob_HTranslationVsSpeedFactor.Evaluate(speedFactor);

                float sinProgress = Mathf.Sin(HeadbobProgress * Mathf.PI * 2f);

                // update the camera location
                defaultCameraOffset += Vector3.up * sinProgress * maxVTranslation;
                defaultCameraOffset += Vector3.right * sinProgress * maxHTranslation;
            }
            else
                HeadbobProgress = 0f;

            LinkedCamera.transform.localPosition = Vector3.MoveTowards(LinkedCamera.transform.localPosition,
                                                                       defaultCameraOffset,
                                                                       Config.Headbob_TranslationBlendSpeed * Time.deltaTime);
        }

        // tilt the camera
        CurrentCameraPitch = Mathf.Clamp(CurrentCameraPitch + cameraPitchDelta,
                                         Config.Camera_MinPitch,
                                         Config.Camera_MaxPitch);
        LinkedCamera.transform.localRotation = Quaternion.Euler(CurrentCameraPitch, 0f, 0f);
    }

    public void SetCursorLock(bool locked)
    {
        Cursor.visible = !locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
    }
}
