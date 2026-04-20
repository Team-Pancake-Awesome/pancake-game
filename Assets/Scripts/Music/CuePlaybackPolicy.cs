using System;

public enum CuePlaybackMode
{
    /// <summary>
    /// Track the cue as normal, allowing it to play and interrupt other cues as needed.
    /// </summary>
    TrackCue,

    /// <summary>
    /// Start playback without registering this cue as the current tracked cue.
    /// Existing tracked cues continue to be treated as currently playing.
    /// </summary>
    IgnorePlayingCues,

    /// <summary>
    /// Do not start playback when another tracked cue is currently playing.
    /// </summary>
    YieldToPlayingCue
}

public readonly struct CuePlaybackPolicy<TCue> where TCue : struct, Enum
{
    public CuePlaybackMode Mode { get; }

    private CuePlaybackPolicy(CuePlaybackMode mode)
    {
        Mode = mode;
    }

    /// <summary>
    /// Creates a policy that tracks this cue as the currently playing cue.
    /// </summary>
    public static CuePlaybackPolicy<TCue> TrackCue => new(CuePlaybackMode.TrackCue);

    /// <summary>
    /// Creates a policy that plays this cue without affecting tracked-cue state.
    /// </summary>
    public static CuePlaybackPolicy<TCue> IgnorePlayingCues => new(CuePlaybackMode.IgnorePlayingCues);

    /// <summary>
    /// Creates a policy that only plays this cue when no tracked cue is currently playing.
    /// </summary>
    public static CuePlaybackPolicy<TCue> YieldToPlayingCue => new(CuePlaybackMode.YieldToPlayingCue);
}
