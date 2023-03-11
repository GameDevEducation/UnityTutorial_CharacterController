using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;

public class HapticsEffectAsset : PlayableAsset
{
    #pragma warning disable 0649
    [SerializeField] HapticEffect Effect;
    [SerializeField] bool OverrideBlendMode = false;
    [SerializeField] HapticEffect.EBlendMode BlendingMode;
    #pragma warning restore 0649

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<HapticsEffectBehaviour>.Create(graph);

        var hapticsEffectBehaviour = playable.GetBehaviour();
        hapticsEffectBehaviour.Effect = Effect;
        hapticsEffectBehaviour.OverrideBlendMode = OverrideBlendMode;
        hapticsEffectBehaviour.BlendingMode = BlendingMode;

        return playable;
    }
}
