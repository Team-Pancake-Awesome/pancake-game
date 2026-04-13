using UnityEngine;
using UnityEngine.InputSystem;

using UnityEngine.InputSystem.Controls;

public class GamepadSpatulaInput : MonoBehaviour, ISpatulaInput, ISpatulaInputBackgroundActivity
{
    [Header("Device Selection")]
    [Tooltip("Optional filter to help identify your custom device by name or display name.")]
    public string deviceNameContains = "";

    [Header("Debug")]
    [Tooltip("Allows keyboard lock input as a debug fallback while using HID")]
    public bool enableKeyboardLockFallback = false;

    [Header("Gamepad Mapping")]
    public bool useRightStickXForPot = true;
    public bool invertRoll = true;

    [Header("Arduino Controls")]
    public KeyCode lockKey = KeyCode.Space;
    public float minPitchInput = 30f;
    public float maxPitchInput = -40f;
    public float rollDeadzone = 2f;

    [Header("Gesture Tuning")]
    public float pitchFlipVelocityThreshold = 90f;
    [Tooltip("Minimum per-packet pitch change in degrees required before a flip can trigger")]
    public float pitchDeltaThreshold = 2.5f;
    [Range(0.01f, 1f)]
    [Tooltip("Smoothing for pitch velocity. Lower values reduce noise but feel less snappy")]
    public float pitchVelocitySmoothing = 0.25f;
    public float rollLimit = 45f;
    public float cooldown = 0.35f;

    [Header("HID Range Decoding")]
    [Tooltip("Expected physical pitch range represented by the HID Y axis.")]
    public float hidPitchRange = 90f;
    [Tooltip("Expected physical roll range represented by the HID X axis.")]
    public float hidRollRange = 90f;

    [Header("Gesture Debug")]
    public float debugGyroY;
    public float debugPitch;
    public float debugPitchDelta;
    public float debugPitchVelocity;
    public float debugRoll;
    public bool isConnected;

    private float lastFlipTime = -999f;
    private bool lastActionButtonHeld;
    private float lastPitchSample;
    private float lastPitchSampleTime = -1f;
    private float latestPitchDelta;
    private float filteredPitchVelocity;

    public bool IsBackgroundActivityEnabled { get; set; } = true;

    public bool TryGetControlState(out SpatulaControlState state)
    {
        state = default;

        if (!IsBackgroundActivityEnabled)
        {
            isConnected = false;
            return false;
        }

        InputDevice device = FindPreferredDevice();
        isConnected = device != null;

        if (device == null)
            return false;

        Vector2 stick = ReadPrimaryStick(device);
        float potValue = ReadPotValue(device);
        bool currentActionButtonHeld = ReadActionButton(device);

        float pitch = stick.y * hidPitchRange;
        float roll = stick.x * hidRollRange;
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

        bool flipArmHeld = currentActionButtonHeld || keyboardHeld;
        bool isFlickingUp = filteredPitchVelocity >= pitchFlipVelocityThreshold;
        bool pitchDeltaOK = Mathf.Abs(latestPitchDelta) >= pitchDeltaThreshold;
        bool rollOK = Mathf.Abs(currentRoll) <= rollLimit;
        bool cooldownOK = (Time.time - lastFlipTime) >= cooldown;

        if (flipArmHeld && isFlickingUp && pitchDeltaOK && rollOK && cooldownOK)
        {
            lastFlipTime = Time.time;
            float strength = Mathf.Clamp(filteredPitchVelocity / Mathf.Max(0.1f, pitchFlipVelocityThreshold), 1f, 2.5f);
            state.FlipTriggered = true;
            state.SnapRequested = true;
            state.FlipStrength = strength;
        }

        return true;
    }

    InputDevice FindPreferredDevice()
    {
        if (!string.IsNullOrWhiteSpace(deviceNameContains))
        {
            string needle = deviceNameContains.ToLowerInvariant();

            foreach (var device in InputSystem.devices)
            {
                string name = (device.name ?? string.Empty).ToLowerInvariant();
                string displayName = (device.displayName ?? string.Empty).ToLowerInvariant();
                string product = (device.description.product ?? string.Empty).ToLowerInvariant();

                if (name.Contains(needle) || displayName.Contains(needle) || product.Contains(needle))
                {
                    if (device is Gamepad || device is Joystick)
                        return device;
                }
            }
        }

        if (Gamepad.current != null)
            return Gamepad.current;

        if (Joystick.current != null)
            return Joystick.current;

        return null;
    }

    Vector2 ReadPrimaryStick(InputDevice device)
    {
        if (device is Gamepad gamepad)
            return gamepad.leftStick.ReadValue();

        if (device is Joystick joystick)
            return joystick.stick.ReadValue();

        return Vector2.zero;
    }

    float ReadPotValue(InputDevice device)
    {
        if (device is Gamepad gamepad)
        {
            float raw = useRightStickXForPot ? gamepad.rightStick.x.ReadValue() : gamepad.rightStick.y.ReadValue();
            return (raw + 1f) * 0.5f;
        }

        if (device is Joystick joystick)
        {
            AxisControl axis = joystick.trigger;
            if (axis != null)
                return Mathf.Clamp01(axis.ReadValue());
        }

        return 0f;
    }

    bool ReadActionButton(InputDevice device)
    {
        if (device is Gamepad gamepad)
            return gamepad.buttonSouth.isPressed;

        if (device is Joystick joystick)
        {
            if (joystick.trigger != null && joystick.trigger.ReadValue() > 0.5f)
                return true;

            for (int i = 0; i < joystick.allControls.Count; i++)
            {
                if (joystick.allControls[i] is ButtonControl button && button.isPressed)
                    return true;
            }
        }

        return false;
    }
}