using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Text;

public class JoystickAxisLogger : MonoBehaviour
{
    public string deviceNameContains = "TinyUSB";
    public float logInterval = 0.5f;

    private float nextLogTime;

    void Update()
    {
        if (Time.time < nextLogTime)
            return;

        nextLogTime = Time.time + Mathf.Max(0.1f, logInterval);

        Joystick joystick = FindPreferredJoystick();
        if (joystick == null)
        {
            Debug.LogWarning("[JoystickAxisLogger] No joystick found.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[JoystickAxisLogger] " + (joystick.displayName ?? joystick.name));

        for (int i = 0; i < joystick.allControls.Count; i++)
        {
            if (joystick.allControls[i] is AxisControl axis)
            {
                sb.AppendLine(axis.name + ": " + axis.ReadValue().ToString("F3"));
            }
        }

        Debug.Log(sb.ToString());
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
}