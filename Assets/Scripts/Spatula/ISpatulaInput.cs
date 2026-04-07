public struct SpatulaControlState
{
    public float PotValue;
    public float HorizontalInput;
    public float PitchNormalized;
    public bool LockPressed;
    public bool LockHeld;
    public bool LockReleased;
    public bool SnapRequested;
    public bool FlipTriggered;
    public float FlipStrength;
}

public interface ISpatulaInput
{
    bool TryGetControlState(out SpatulaControlState state);
}

public interface ISpatulaInputBackgroundActivity
{
    bool IsBackgroundActivityEnabled { get; set; }
}