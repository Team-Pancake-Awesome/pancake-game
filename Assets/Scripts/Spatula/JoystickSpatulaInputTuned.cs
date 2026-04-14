using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class JoystickSpatulaInputTuned : MonoBehaviour, ISpatulaInput, ISpatulaInputBackgroundActivity
{
    [Header("Device Selection")]
    [Tooltip("Optional filter to help identify your custom device by name or display name.")]
    public string deviceNameContains = "TinyUSB";

    [Header("Debug")]
    [Tooltip("Allows keyboard lock input as a debug fallback while using HID")]
    public bool enableKeyboardLockFallback = false;
    [Tooltip("Logs live joystick values at this interval while running.")]
    public bool logLiveValues = false;
    public float logInterval = 1f;

    [Header("Joystick Mapping")]
    public bool invertRoll = true;
    public bool invertPitch = true;
    [Tooltip("Expected physical pitch range represented by the HID stick Y axis.")]
    public float hidPitchRange = 90f;
    [Tooltip("Expected physical roll range represented by the HID stick X axis.")]
    public float hidRollRange = 90f;
    [Tooltip("Ignore small stick movement near center.")]
    [Range(0f, 1f)]
    public float stickDeadzone = 0.12f;
    [Tooltip("Extra trim to apply after HID decoding.")]
    public float pitchOffset = 0f;
    [Tooltip("Extra trim to apply after HID decoding.")]
    public float rollOffset = 0f;

    [Tooltip("Specific axis control name to use for potentiometer, e.g. 'z' or 'rz'. Leave blank to use fallbacks.")]
    public string potControlName = "z";
    [Tooltip("Use joystick trigger as the potentiometer value when available.")]
    public bool useTriggerForPot = true;
    [Tooltip("Observed minimum raw value for the potentiometer axis.")]
    public float potAxisMin = -0.85f;
    [Tooltip("Observed maximum raw value for the potentiometer axis.")]
    public float potAxisMax = 0.85f;
    public bool invertPot = false;
    [Tooltip("Fallback potentiometer value if the joystick does not expose a usable axis.")]
    [Range(0f, 1f)]
    public float fallbackPotValue = 0f;

    [Header("Action Button Mapping")]
    [Tooltip("Specific joystick button name to use for action. Leave blank to accept any non-directional button.")]
    public string actionButtonName = "";
    [Tooltip("Use the joystick trigger as the lock/action input when available.")]
    public bool useTriggerForAction = false;

    [Header("Arduino Controls")]
    public KeyCode lockKey = KeyCode.Space;
    public float minPitchInput = 30f;
    public float maxPitchInput = -40f;
    public float rollDeadzone = 2f;

    [Header("Gesture Tuning")]
    public bool enableFlipDetection = false;
    public float pitchFlipVelocityThreshold = 90f;
    [Tooltip("Minimum per-packet pitch change in degrees required before a flip can trigger")]
    public float pitchDeltaThreshold = 2.5f;
    [Range(0.01f, 1f)]
    [Tooltip("Smoothing for pitch velocity. Lower values reduce noise but feel less snappy")]
    public float pitchVelocitySmoothing = 0.25f;
    public float rollLimit = 45f;
    public float cooldown = 0.35f;

    [Header("Gesture Debug")]
    public float debugGyroY;
    public float debugPitch;
    public float debugPitchDelta;
    public float debugPitchVelocity;
    public float debugRoll;
    public bool isConnected;
    public string connectedDeviceName;
    public Vector2 debugStick;
    public float debugTrigger;
    public float debugPotAxis;
    public bool debugAnyButtonPressed;
    public string debugPressedButtonName;
    public string debugPotControlName;

    private float lastFlipTime = -999f;
    private bool lastActionButtonHeld;
    private float lastPitchSample;
    private float lastPitchSampleTime = -1f;
    private float latestPitchDelta;
    private float filteredPitchVelocity;
    private float nextLogTime;

    public bool IsBackgroundActivityEnabled { get; set; } = true;

    public bool TryGetControlState(out SpatulaControlState state)
    {
        state = default;

        if (!IsBackgroundActivityEnabled)
        {
            isConnected = false;
            connectedDeviceName = string.Empty;
            return false;
        }

        Joystick joystick = FindPreferredJoystick();
        isConnected = joystick != null;
        connectedDeviceName = joystick != null ? (joystick.displayName ?? joystick.name) : string.Empty;

        if (joystick == null)
            return false;

        Vector2 stick = joystick.stick.ReadValue();
        stick = ApplyStickDeadzone(stick);

        float potValue = ReadPotValue(joystick);
        bool currentActionButtonHeld = ReadActionButton(joystick);

        float pitch = stick.y * hidPitchRange + pitchOffset;
        if (invertPitch)
            pitch = -pitch;

        float roll = stick.x * hidRollRange + rollOffset;
        float currentRoll = invertRoll ? -roll : roll;

        if (lastPitchSampleTime < 0f)
        {
            lastPitchSample = pitch;
            lastPitchSampleTime = Time.time;
            latestPitchDelta = 0f;
            filteredPitchVelocity = 0f;
        }
        else
        {
            float sampleDeltaTime = Mathf.Max(0.0001f, Time.time - lastPitchSampleTime);
            latestPitchDelta = pitch - lastPitchSample;
            float rawPitchVelocity = latestPitchDelta / sampleDeltaTime;
            float smoothing = Mathf.Clamp01(pitchVelocitySmoothing);
            filteredPitchVelocity = Mathf.Lerp(filteredPitchVelocity, rawPitchVelocity, smoothing);

            lastPitchSample = pitch;
            lastPitchSampleTime = Time.time;
        }

        debugGyroY = 0f;
        debugPitch = pitch;
        debugPitchDelta = latestPitchDelta;
        debugPitchVelocity = filteredPitchVelocity;
        debugRoll = currentRoll;
        debugStick = stick;
        debugTrigger = joystick.trigger != null ? joystick.trigger.ReadValue() : 0f;

        state.PotValue = Mathf.Clamp01(potValue);
        state.HorizontalInput = Mathf.Abs(currentRoll) > rollDeadzone ? currentRoll : 0f;

        float normalizedPitch = Mathf.InverseLerp(minPitchInput, maxPitchInput, pitch);
        state.PitchNormalized = Mathf.Clamp01(normalizedPitch);

        bool keyboardPressed = enableKeyboardLockFallback && Input.GetKeyDown(lockKey);
        bool keyboardHeld = enableKeyboardLockFallback && Input.GetKey(lockKey);
        bool keyboardReleased = enableKeyboardLockFallback && Input.GetKeyUp(lockKey);

        state.LockPressed = keyboardPressed || (currentActionButtonHeld && !lastActionButtonHeld);
        state.LockHeld = keyboardHeld || currentActionButtonHeld;
        state.LockReleased = keyboardReleased || (!currentActionButtonHeld && lastActionButtonHeld);

        lastActionButtonHeld = currentActionButtonHeld;

        if (enableFlipDetection)
        {
            bool flipArmHeld = currentActionButtonHeld || keyboardHeld;
            bool isFlickingUp = filteredPitchVelocity >= pitchFlipVelocityThreshold;
            bool pitchDeltaOK = Mathf.Abs(latestPitchDelta) >= pitchDeltaThreshold;
            bool rollOK = Mathf.Abs(currentRoll) <= rollLimit;
            bool cooldownOK = (Time.time - lastFlipTime) >= cooldown;

            if (flipArmHeld && isFlickingUp && pitchDeltaOK && rollOK && cooldownOK)
            {
                lastFlipTime = Time.time;
                float strength = Mathf.Clamp(filteredPitchVelocity / Mathf.Max(0.1f, pitchFlipVelocityThreshold), 1f, 2.5f);
                Debug.Log($"FLIP DETECTED | Velocity: {filteredPitchVelocity:F1} | Delta: {latestPitchDelta:F1} | Strength: {strength:F2}");
                state.FlipTriggered = true;
                state.SnapRequested = true;
                state.FlipStrength = strength;
            }
        }

        if (logLiveValues && Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + Mathf.Max(0.1f, logInterval);
            Debug.Log(
                "[JoystickSpatulaInputTuned] " +
                connectedDeviceName +
                " | stick: " + stick +
                " | trigger: " + debugTrigger.ToString("F2") +
                " | pot: " + state.PotValue.ToString("F2") +
                " | potRaw: " + debugPotAxis.ToString("F2") +
                " | potControl: " + debugPotControlName +
                " | button: " + currentActionButtonHeld +
                " | pressedButton: " + debugPressedButtonName +
                " | pitch: " + pitch.ToString("F1") +
                " | roll: " + currentRoll.ToString("F1"));
        }

        return true;
    }

    Joystick FindPreferredJoystick()
    {
        if (!string.IsNullOrWhiteSpace(deviceNameContains))
        {
            string needle = deviceNameContains.ToLowerInvariant();

            foreach (var device in InputSystem.devices)
            {
                if (device is not Joystick joystick)
                    continue;

                string name = (joystick.name ?? string.Empty).ToLowerInvariant();
                string displayName = (joystick.displayName ?? string.Empty).ToLowerInvariant();
                string product = (joystick.description.product ?? string.Empty).ToLowerInvariant();

                if (name.Contains(needle) || displayName.Contains(needle) || product.Contains(needle))
                    return joystick;
            }
        }

        return Joystick.current;
    }

    Vector2 ApplyStickDeadzone(Vector2 value)
    {
        if (Mathf.Abs(value.x) < stickDeadzone)
            value.x = 0f;

        if (Mathf.Abs(value.y) < stickDeadzone)
            value.y = 0f;

        return value;
    }

    float ReadPotValue(Joystick joystick)
    {
        debugPotAxis = 0f;
        debugPotControlName = string.Empty;

        if (!string.IsNullOrWhiteSpace(potControlName))
        {
            for (int i = 0; i < joystick.allControls.Count; i++)
            {
                if (joystick.allControls[i] is not AxisControl axis)
                    continue;

                if (!string.Equals(axis.name, potControlName, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                float value = axis.ReadValue();
                debugPotAxis = value;
                debugPotControlName = axis.name;

                float normalized = Mathf.InverseLerp(potAxisMin, potAxisMax, value);
                if (invertPot)
                    normalized = 1f - normalized;

                return Mathf.Clamp01(normalized);
            }
        }

        if (useTriggerForPot && joystick.trigger != null)
        {
            float triggerValue = joystick.trigger.ReadValue();
            if (triggerValue > 0.0001f)
            {
                debugPotAxis = triggerValue;
                debugPotControlName = "trigger";
                return Mathf.Clamp01(triggerValue);
            }
        }

        for (int i = 0; i < joystick.allControls.Count; i++)
        {
            if (joystick.allControls[i] is not AxisControl axis)
                continue;

            if (axis.name == "x" || axis.name == "y")
                continue;

            float value = axis.ReadValue();
            if (Mathf.Abs(value) < 0.01f)
                continue;

            debugPotAxis = value;
            debugPotControlName = axis.name;

            float normalized = Mathf.InverseLerp(potAxisMin, potAxisMax, value);
            if (invertPot)
                normalized = 1f - normalized;

            return Mathf.Clamp01(normalized);
        }

        return Mathf.Clamp01(fallbackPotValue);
    }

    bool ReadActionButton(Joystick joystick)
    {
        debugAnyButtonPressed = false;
        debugPressedButtonName = string.Empty;

        if (useTriggerForAction && joystick.trigger != null && joystick.trigger.ReadValue() > 0.5f)
        {
            debugAnyButtonPressed = true;
            debugPressedButtonName = "trigger";
            return true;
        }

        for (int i = 0; i < joystick.allControls.Count; i++)
        {
            if (joystick.allControls[i] is not ButtonControl button || !button.isPressed)
                continue;

            debugAnyButtonPressed = true;
            debugPressedButtonName = button.name;

            if (IsDirectionalButtonName(button.name))
                continue;

            if (!string.IsNullOrWhiteSpace(actionButtonName) &&
                !string.Equals(button.name, actionButtonName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            return true;
        }

        return false;
    }

    bool IsDirectionalButtonName(string buttonName)
    {
        if (string.IsNullOrEmpty(buttonName))
            return false;

        return
            string.Equals(buttonName, "up", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(buttonName, "down", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(buttonName, "left", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(buttonName, "right", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(buttonName, "hatswitch", System.StringComparison.OrdinalIgnoreCase);
    }
}