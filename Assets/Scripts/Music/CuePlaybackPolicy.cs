using System;

public enum CuePlaybackMode
{
    TrackCue,
    IgnorePlayingCues,
    YieldToPlayingCue
}

public readonly struct CuePlaybackPolicy<TCue> where TCue : struct, Enum
{
    public CuePlaybackMode Mode { get; }

    private CuePlaybackPolicy(CuePlaybackMode mode)
    {
        Mode = mode;
    }

    public static CuePlaybackPolicy<TCue> TrackCue => new(CuePlaybackMode.TrackCue);
    public static CuePlaybackPolicy<TCue> IgnorePlayingCues => new(CuePlaybackMode.IgnorePlayingCues);
    public static CuePlaybackPolicy<TCue> YieldToPlayingCue => new(CuePlaybackMode.YieldToPlayingCue);
}
