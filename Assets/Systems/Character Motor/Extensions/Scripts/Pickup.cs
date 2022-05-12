using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Events;

public class Pickup : MonoBehaviour
{
    [Header("General")]
    [SerializeField] protected Transform MeshRootObject;
    [SerializeField] protected Transform UIRootObject;
    [SerializeField] protected GameObject PromptCanvas;
    [SerializeField] protected CanvasGroup UIGroup;
    [SerializeField] protected TextMeshProUGUI DescriptionText;
    [SerializeField] protected TextMeshProUGUI InstructionText;

    [Header("Options")]
    [SerializeField] protected bool DespawnOnPickup = true;
    [field: SerializeField] public bool PickupOnContact { get; protected set; } = false;
    [field: SerializeField] public bool HasPickupPrompt { get; protected set; } = false;

    [Header("Movement")]
    [SerializeField] protected float RotationSpeed = 30f;
    [SerializeField] protected float BounceOffset = 0.5f;
    [SerializeField] protected float BounceAmplitude = 0.25f;
    [SerializeField] protected float BouncePeriod = 4f;

    [Header("Prompt")]
    [SerializeField] protected float FadeInTime = 0.1f;
    [SerializeField] protected float FadeDelay = 0.5f;
    [SerializeField] protected float FadeOutTime = 0.1f;

    [Header("Events")]
    [SerializeField] protected UnityEvent<Pickup> OnPickedUp = new UnityEvent<Pickup>();

    enum EState
    {
        Idle,
        StartingToLookAt,
        LookingAt,
        StoppingLookingAt
    }

    EState CurrentState = EState.Idle;
    float TransitionProgress;
    float FadeOutCooldownRemaining;

    // Start is called before the first frame update
    void Start()
    {
        InstructionText.gameObject.SetActive(HasPickupPrompt && !PickupOnContact);
    }

    // Update is called once per frame
    void Update()
    {
        MeshRootObject.Rotate(0f, RotationSpeed * Time.deltaTime, 0f);

        float bounceProgress = (Time.time % BouncePeriod) / BouncePeriod;
        float heightOffset = BounceOffset + BounceAmplitude * Mathf.Sin(bounceProgress * Mathf.PI * 2f);
        MeshRootObject.localPosition = Vector3.up * heightOffset;

        if (HasPickupPrompt)
        {
            if (CurrentState == EState.StartingToLookAt)
            {
                TransitionProgress = Mathf.Clamp01(TransitionProgress + Time.deltaTime / FadeInTime);
                UIGroup.alpha = TransitionProgress;
                PromptCanvas.SetActive(true);

                if (TransitionProgress >= 1f)
                    CurrentState = EState.LookingAt;
            }
            else if (CurrentState == EState.StoppingLookingAt)
            {
                if (FadeOutCooldownRemaining >= 0f)
                    FadeOutCooldownRemaining -= Time.deltaTime;

                if (FadeOutCooldownRemaining <= 0f)
                {
                    TransitionProgress = Mathf.Clamp01(TransitionProgress + Time.deltaTime / FadeOutTime);
                    UIGroup.alpha = 1f - TransitionProgress;

                    if (TransitionProgress >= 1f)
                    {
                        PromptCanvas.SetActive(false);
                        CurrentState = EState.Idle;
                    }
                }
            }

            if (UIGroup.alpha > 0f)
            {
                UIRootObject.LookAt(Camera.main.transform.position);
            }
        }
    }

    public virtual bool CanPickup()
    {
        return true;
    }

    public virtual void PerformPickup()
    {
        OnPickedUp.Invoke(this);

        if (DespawnOnPickup)
            Destroy(gameObject);
    }

    public virtual void OnStartLookingAt()
    {
        CurrentState = EState.StartingToLookAt;
        TransitionProgress = 0f;
    }

    public virtual void OnStopLookingAt()
    {
        CurrentState = EState.StoppingLookingAt;
        TransitionProgress = 0f;
        FadeOutCooldownRemaining = FadeDelay;
    }

    public virtual void OnContinueLookingAt()
    {
    }
}
