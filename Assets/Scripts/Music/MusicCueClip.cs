using System;
using UnityEngine;

[Serializable]
public class MusicCueClip : CueClip
{
    [SerializeField]
    private MusicCues cue;

    public override int CueId => (int)cue;

    [Tooltip("If true, this music will play on top of any existing music instead of replacing it")]
    public bool additive = false;

    [Tooltip("If additive, how long to take to transition in this music on top of the existing music")]
    public float additiveTransitionTime = 0f;
}
