using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;

public class HapticsEffectBehaviour : PlayableBehaviour
{
    public HapticEffect Effect;
    public bool OverrideBlendMode = false;
    public HapticEffect.EBlendMode BlendingMode;

    private bool HasPlayed = false;
    private System.Guid PlayingID = System.Guid.Empty;
    private HapticsManager AssociatedManager;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        HasPlayed = false;
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        if (AssociatedManager != null && PlayingID != System.Guid.Empty)
        {
            AssociatedManager.StopEffect(PlayingID);            
        }

        AssociatedManager = null;
        PlayingID = System.Guid.Empty;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        AssociatedManager = playerData as HapticsManager;

        if (AssociatedManager != null && !HasPlayed)
        {
            HasPlayed = true;
            AssociatedManager.PlayEffect(Effect, (float)playable.GetDuration(), OverrideBlendMode ? BlendingMode : Effect.Blending, out PlayingID);
        }
    }
}
