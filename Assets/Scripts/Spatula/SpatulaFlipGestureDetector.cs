using UnityEngine;

public enum SpatulaFlipDetectionMode
{
    LegacyPitchVelocity,
    GestureWindowPitchVelocity,
    GestureWindowGyro
}

public struct SpatulaFlipGestureResult
{
    public bool Triggered;
    public float Strength;
    public float SignedMotion;
    public float PeakMotion;
    public float RollAtPeak;
    public string StrengthLabel;
}

[System.Serializable]
public class SpatulaFlipGestureDetector
{
    [Header("Detection")]
    [Tooltip("Positive means the raw signal must go positive. Negative means the raw signal must go negative.")]
    public int validFlipDirection = -1;

    [Tooltip("Minimum signed speed needed to count as an intentional flip.")]
    public float minimumFlipSpeed = 240f;

    [Tooltip("Speed that maps to maximum flip strength.")]
    public float fullPowerFlipSpeed = 1200f;

    [Tooltip("How far back to search for the peak motion when grading strength.")]
    public float strengthLookbackSeconds = 0.14f;

    [Tooltip("Cooldown after a confirmed flip.")]
    public float cooldown = 0.35f;

    [Header("Gesture Window")]
    [Tooltip("Wait a tiny moment after the first threshold crossing so the detector can capture the actual peak instead of launching on the weakest first frame.")]
    public bool waitForPeakBeforeLaunch = true;

    [Tooltip("How long to collect motion after the first threshold crossing before confirming the flip.")]
    public float confirmationWindowSeconds = 0.08f;

    [Tooltip("If true, releasing the action button cancels a pending flip candidate. For the physical spatula this is usually false so quick releases do not eat valid flips.")]
    public bool cancelCandidateWhenArmReleased = false;

    [Header("Strength Output")]
    public float lightStrength = 0.85f;
    public float maxStrength = 2.0f;

    [Header("Roll Handling")]
    [Tooltip("Roll no longer blocks by default. It can slightly reduce strength so twisted flips feel sloppier without feeling broken.")]
    public bool rollBlocksFlip = false;

    public float rollBlockLimit = 85f;
    public float rollCleanLimit = 45f;
    public float rollPenaltyAt = 90f;

    [Range(0f, 0.5f)]
    public float maxRollStrengthPenalty = 0.12f;

    [Header("Debug")]
    public bool logDetectedFlips = true;
    public bool logRejectedCandidates = false;
    public float rejectedLogInterval = 0.25f;

    const int SampleCapacity = 64;

    float[] sampleTimes;
    float[] signedMotionSamples;
    float[] rollSamples;
    int sampleIndex;
    bool samplesInitialized;
    float lastFlipTime = -999f;
    float nextRejectedLogTime;

    bool candidateActive;
    float candidateStartTime;
    float candidateBestMotion;
    float candidateRollAtPeak;
    SpatulaFlipDetectionMode candidateMode;

    public void ResetRuntimeState()
    {
        sampleIndex = 0;
        samplesInitialized = false;
        lastFlipTime = -999f;
        nextRejectedLogTime = 0f;

        candidateActive = false;
        candidateStartTime = 0f;
        candidateBestMotion = 0f;
        candidateRollAtPeak = 0f;
    }

    public bool TryDetect(
        float rawMotion,
        float currentRoll,
        bool flipArmHeld,
        SpatulaFlipDetectionMode mode,
        out SpatulaFlipGestureResult result)
    {
        EnsureSamples();

        result = default;

        float now = Time.time;
        float direction = validFlipDirection < 0 ? -1f : 1f;
        float signedMotion = rawMotion * direction;

        AddSample(now, signedMotion, currentRoll);

        if (candidateActive)
        {
            UpdateCandidate(signedMotion, currentRoll);

            if (cancelCandidateWhenArmReleased && !flipArmHeld)
            {
                LogRejectedCandidate(now, candidateBestMotion, candidateRollAtPeak, "released-during-window", candidateMode);
                ClearCandidate();
                return false;
            }

            if (now - candidateStartTime < Mathf.Max(0.01f, confirmationWindowSeconds))
            {
                return false;
            }

            return ConfirmCandidate(now, out result);
        }

        if (!flipArmHeld)
        {
            return false;
        }

        if (now - lastFlipTime < cooldown)
        {
            LogRejectedCandidate(now, signedMotion, currentRoll, "cooldown", mode);
            return false;
        }

        if (signedMotion < minimumFlipSpeed)
        {
            LogRejectedCandidate(now, signedMotion, currentRoll, "below-threshold", mode);
            return false;
        }

        if (rollBlocksFlip && Mathf.Abs(currentRoll) > rollBlockLimit)
        {
            LogRejectedCandidate(now, signedMotion, currentRoll, "roll-blocked", mode);
            return false;
        }

        if (waitForPeakBeforeLaunch)
        {
            StartCandidate(now, signedMotion, currentRoll, mode);
            return false;
        }

        return ConfirmImmediate(now, signedMotion, currentRoll, mode, out result);
    }

    void StartCandidate(float now, float signedMotion, float currentRoll, SpatulaFlipDetectionMode mode)
    {
        candidateActive = true;
        candidateStartTime = now;
        candidateBestMotion = signedMotion;
        candidateRollAtPeak = currentRoll;
        candidateMode = mode;
    }

    void UpdateCandidate(float signedMotion, float currentRoll)
    {
        if (signedMotion > candidateBestMotion)
        {
            candidateBestMotion = signedMotion;
            candidateRollAtPeak = currentRoll;
        }
    }

    bool ConfirmCandidate(float now, out SpatulaFlipGestureResult result)
    {
        float lookbackPeak;
        float lookbackRoll;
        GetPeakInLookback(now, out lookbackPeak, out lookbackRoll);

        float peakMotion = candidateBestMotion;
        float rollAtPeak = candidateRollAtPeak;

        if (lookbackPeak > peakMotion)
        {
            peakMotion = lookbackPeak;
            rollAtPeak = lookbackRoll;
        }

        SpatulaFlipDetectionMode mode = candidateMode;
        ClearCandidate();

        if (peakMotion < minimumFlipSpeed)
        {
            result = default;
            LogRejectedCandidate(now, peakMotion, rollAtPeak, "candidate-below-threshold", mode);
            return false;
        }

        if (rollBlocksFlip && Mathf.Abs(rollAtPeak) > rollBlockLimit)
        {
            result = default;
            LogRejectedCandidate(now, peakMotion, rollAtPeak, "candidate-roll-blocked", mode);
            return false;
        }

        return BuildResult(now, peakMotion, rollAtPeak, mode, out result);
    }

    bool ConfirmImmediate(float now, float signedMotion, float currentRoll, SpatulaFlipDetectionMode mode, out SpatulaFlipGestureResult result)
    {
        float peakMotion = signedMotion;
        float rollAtPeak = currentRoll;
        GetPeakInLookback(now, out peakMotion, out rollAtPeak);

        return BuildResult(now, peakMotion, rollAtPeak, mode, out result);
    }

    bool BuildResult(float now, float peakMotion, float rollAtPeak, SpatulaFlipDetectionMode mode, out SpatulaFlipGestureResult result)
    {
        result = default;

        float strengthT = Mathf.InverseLerp(minimumFlipSpeed, Mathf.Max(minimumFlipSpeed + 1f, fullPowerFlipSpeed), peakMotion);
        float strength = Mathf.Lerp(lightStrength, maxStrength, strengthT);

        float rollPenaltyT = Mathf.InverseLerp(rollCleanLimit, Mathf.Max(rollCleanLimit + 1f, rollPenaltyAt), Mathf.Abs(rollAtPeak));
        strength -= rollPenaltyT * maxRollStrengthPenalty;
        strength = Mathf.Clamp(strength, lightStrength, maxStrength);

        string strengthLabel = GetStrengthLabel(strengthT);

        lastFlipTime = now;

        result.Triggered = true;
        result.Strength = strength;
        result.SignedMotion = peakMotion;
        result.PeakMotion = peakMotion;
        result.RollAtPeak = rollAtPeak;
        result.StrengthLabel = strengthLabel;

        if (logDetectedFlips)
        {
            Debug.Log(
                $"FLIP DETECTED [{strengthLabel}] | mode={mode} | motion={peakMotion:F1} | peak={peakMotion:F1} | roll={rollAtPeak:F1} | strength={strength:F2}"
            );
        }

        return true;
    }

    void ClearCandidate()
    {
        candidateActive = false;
        candidateStartTime = 0f;
        candidateBestMotion = 0f;
        candidateRollAtPeak = 0f;
    }

    void EnsureSamples()
    {
        if (sampleTimes != null && signedMotionSamples != null && rollSamples != null)
        {
            return;
        }

        sampleTimes = new float[SampleCapacity];
        signedMotionSamples = new float[SampleCapacity];
        rollSamples = new float[SampleCapacity];
        sampleIndex = 0;
        samplesInitialized = false;
    }

    void AddSample(float time, float signedMotion, float roll)
    {
        sampleTimes[sampleIndex] = time;
        signedMotionSamples[sampleIndex] = signedMotion;
        rollSamples[sampleIndex] = roll;

        sampleIndex = (sampleIndex + 1) % SampleCapacity;

        if (sampleIndex == 0)
        {
            samplesInitialized = true;
        }
    }

    void GetPeakInLookback(float now, out float peakMotion, out float rollAtPeak)
    {
        float lookbackStart = now - Mathf.Max(0.01f, strengthLookbackSeconds);

        peakMotion = float.MinValue;
        rollAtPeak = 0f;

        int count = samplesInitialized ? SampleCapacity : sampleIndex;

        for (int i = 0; i < count; i++)
        {
            if (sampleTimes[i] < lookbackStart)
            {
                continue;
            }

            if (signedMotionSamples[i] > peakMotion)
            {
                peakMotion = signedMotionSamples[i];
                rollAtPeak = rollSamples[i];
            }
        }

        if (peakMotion == float.MinValue)
        {
            peakMotion = 0f;
            rollAtPeak = 0f;
        }
    }

    void LogRejectedCandidate(float now, float signedMotion, float currentRoll, string reason, SpatulaFlipDetectionMode mode)
    {
        if (!logRejectedCandidates)
        {
            return;
        }

        if (now < nextRejectedLogTime)
        {
            return;
        }

        // Only log near-attempts so the Console does not become useless.
        if (signedMotion < minimumFlipSpeed * 0.45f)
        {
            return;
        }

        nextRejectedLogTime = now + Mathf.Max(0.05f, rejectedLogInterval);

        Debug.Log(
            $"FLIP BLOCKED | reason={reason} | mode={mode} | motion={signedMotion:F1} | roll={currentRoll:F1} | threshold={minimumFlipSpeed:F1}"
        );
    }

    string GetStrengthLabel(float strengthT)
    {
        if (strengthT < 0.30f)
        {
            return "LIGHT";
        }

        if (strengthT < 0.68f)
        {
            return "MEDIUM";
        }

        if (strengthT < 0.95f)
        {
            return "HEAVY";
        }

        return "WILD";
    }
}
