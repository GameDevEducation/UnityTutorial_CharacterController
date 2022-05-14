using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
public class PlayerCharacterMotor : CharacterMotor
{
    [Header("Player")]
    [SerializeField] protected Transform LinkedCamera;

    protected float CurrentCameraPitch = 0f;
    protected float HeadbobProgress = 0f;
    protected float Camera_CurrentTime = 0f;

    public bool SendUIInteractions { get; protected set; } = true;

    #region Input System Handling
    protected virtual void OnMove(InputValue value)
    {
        State.Input_Move = value.Get<Vector2>();
    }

    protected virtual void OnLook(InputValue value)
    {
        State.Input_Look = value.Get<Vector2>();
    }

    protected virtual void OnJump(InputValue value)
    {
        State.Input_Jump = value.isPressed;
    }

    protected virtual void OnRun(InputValue value)
    {
        State.Input_Run = value.isPressed;
    }

    protected virtual void OnCrouch(InputValue value)
    {
        State.Input_Crouch = value.isPressed;
    }

    protected virtual void OnPrimaryAction(InputValue value)
    {
        State.Input_PrimaryAction = value.isPressed;

        // need to inject pointer event
        if (State.Input_PrimaryAction && SendUIInteractions)
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

        if (State.Input_PrimaryAction)
            OnPrimary.Invoke();
    }

    protected virtual void OnSecondaryAction(InputValue value)
    {
        State.Input_SecondaryAction = value.isPressed;

        if (State.Input_SecondaryAction)
            OnSecondary.Invoke();
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

        LinkedCamera.transform.localPosition = Vector3.up * (MovementMode.CurrentHeight + Config.Camera_VerticalOffset);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();

        UpdateCamera();
    }

    protected void UpdateCamera()
    {
        // not allowed to look around?
        if (State.IsLookingLocked)
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
        if (State.CurrentSurfaceSource != null)
        {
            hSensitivity = State.CurrentSurfaceSource.Effect(hSensitivity, EEffectableParameter.CameraSensitivity);
            vSensitivity = State.CurrentSurfaceSource.Effect(vSensitivity, EEffectableParameter.CameraSensitivity);
        }

        // calculate our camera inputs
        float cameraYawDelta = State.Input_Look.x * hSensitivity * Time.deltaTime;
        float cameraPitchDelta = State.Input_Look.y * vSensitivity * Time.deltaTime * (Config.Camera_InvertY ? 1f : -1f);

        // rotate the character
        transform.localRotation = transform.localRotation * Quaternion.Euler(0f, cameraYawDelta, 0f);

        LinkedCamera.transform.localPosition = Vector3.up * (MovementMode.CurrentHeight + Config.Camera_VerticalOffset);

        // headbob enabled and on the ground?
        if (Config.Headbob_Enable && State.IsGrounded)
        {
            float currentSpeed = State.LinkedRB.velocity.magnitude;

            // moving fast enough to bob?
            Vector3 defaultCameraOffset = Vector3.up * (MovementMode.CurrentHeight + Config.Camera_VerticalOffset);
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
