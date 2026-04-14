using System;

[Serializable]
public class MusicCueClip : CueClip
{
    public bool additive = false; // if true, this music will play on top of any existing music instead of replacing it
    public float additiveTransitionTime = 0f;
}
