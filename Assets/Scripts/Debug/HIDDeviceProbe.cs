using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Text;

public class HIDDeviceProbe : MonoBehaviour
{
    [Header("Debug")]
    [Tooltip("Logs the currently connected Input System devices on Start.")]
    public bool logDevicesOnStart = true;
    [Tooltip("Logs live control values at this interval while running.")]
    public float liveLogInterval = 1.0f;
    [Tooltip("Optional filter to help identify your custom device by name or display name.")]
    public string deviceNameContains = "";

    private float nextLiveLogTime;

    void Start()
    {
        if (logDevicesOnStart)
            LogConnectedDevices();
    }

    void Update()
    {
        if (Time.time < nextLiveLogTime)
            return;

        nextLiveLogTime = Time.time + Mathf.Max(0.1f, liveLogInterval);
        LogLiveDeviceState();
    }

    [ContextMenu("Log Connected Devices")]
    public void LogConnectedDevices()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[HIDDeviceProbe] Connected Input Devices:");

        foreach (var device in InputSystem.devices)
        {
            sb.AppendLine(
                "- " + device.GetType().Name +
                " | name: " + device.name +
                " | displayName: " + device.displayName +
                " | layout: " + device.layout +
                " | interface: " + device.description.interfaceName +
                " | product: " + device.description.product +
                " | manufacturer: " + device.description.manufacturer);
        }

        Debug.Log(sb.ToString());
    }

    void LogLiveDeviceState()
    {
        InputDevice device = FindPreferredDevice();
        if (device == null)
        {
            Debug.LogWarning("[HIDDeviceProbe] No matching Gamepad or Joystick found.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[HIDDeviceProbe] Live Device State");
        sb.AppendLine("Type: " + device.GetType().Name);
        sb.AppendLine("Name: " + device.name);
        sb.AppendLine("Display Name: " + device.displayName);
        sb.AppendLine("Layout: " + device.layout);

        if (device is Gamepad gamepad)
        {
            Vector2 leftStick = gamepad.leftStick.ReadValue();
            Vector2 rightStick = gamepad.rightStick.ReadValue();

            sb.AppendLine("Left Stick: " + leftStick);
            sb.AppendLine("Right Stick: " + rightStick);
            sb.AppendLine("buttonSouth: " + gamepad.buttonSouth.isPressed);
            sb.AppendLine("buttonNorth: " + gamepad.buttonNorth.isPressed);
            sb.AppendLine("buttonWest: " + gamepad.buttonWest.isPressed);
            sb.AppendLine("buttonEast: " + gamepad.buttonEast.isPressed);
        }
        else if (device is Joystick joystick)
        {
            Vector2 stick = joystick.stick.ReadValue();

            sb.AppendLine("Stick: " + stick);
            sb.AppendLine("trigger: " + joystick.trigger.ReadValue());

            if (joystick.hatswitch != null)
                sb.AppendLine("hatswitch: " + joystick.hatswitch.ReadValue());

            for (int i = 0; i < joystick.allControls.Count; i++)
            {
                if (joystick.allControls[i] is ButtonControl button && button.isPressed)
                    sb.AppendLine("Pressed Button: " + button.name);
            }
        }

        Debug.Log(sb.ToString());
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
}