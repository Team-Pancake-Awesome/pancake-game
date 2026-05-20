public struct SpatulaControlState
{
    public float HorizontalInput;
    public float PotValue;
    public float PitchNormalized;
    public bool LockPressed;
    public bool LockHeld;
    public bool LockReleased;
    public bool SnapRequested;
    public bool FlipTriggered;
    public float FlipStrength;

    // Debug/telemetry values supplied by the active input source.
    // These are intentionally optional so older/debug inputs can ignore them.
    public float FlipMotion;
    public float FlipRoll;
}

public interface ISpatulaInput
{
    bool TryGetControlState(out SpatulaControlState state);
}

public interface ISpatulaInputBackgroundActivity
{
    bool IsBackgroundActivityEnabled { get; set; }
}
