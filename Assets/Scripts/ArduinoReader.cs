using UnityEngine;
using System.IO.Ports;
using System.Linq;
using System.Globalization;
using System;

public class ArduinoReader : MonoBehaviour, ISpatulaInput
{
    [Header("Serial Port Settings")]
    public string portName = "COM4";
    public int baudRate = 115200;

    private SerialPort serialPort;

    [Header("Pot Data")]
    [Range(0f, 1f)]
    public float sensorValue = 0f;
    public int rawPot = 0;

    [Header("Debug")]
    [Tooltip("Ignore potentiometer input during play for debugging.")]
    public bool ignorePot = false;

    [Header("Gyro Data")]
    public float pitch = 0f;
    public float roll = 0f;
    public float gyroY = 0f; //
    public float accelZ = 0f; // captures upward thrust
    public int actionButton = 0; //space 

    [Header("Arduino Controls")]
    public KeyCode lockKey = KeyCode.Space;
    public float minPitchInput = 30f;
    public float maxPitchInput = -40f;
    public float rollDeadzone = 2f;
    public bool invertRoll = true;

    [Header("Gesture Tuning")]
    public float gyroYThreshold = 1.75f;
    public float rollLimit = 45f;
    public float cooldown = 0.35f;

    [Header("Gesture Debug")]
    public float debugGyroY;
    public float debugRoll;

    private float lastFlipTime = -999f;
    private bool lastActionButtonHeld;

    void Start()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(portName))
                portName = FindPort();

            if (string.IsNullOrWhiteSpace(portName))
            {
                Debug.LogWarning("No serial ports found.");
                return;
            }

            serialPort = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 500
            };

            serialPort.Open();
            serialPort.DiscardInBuffer();
            Debug.Log("Opened: " + portName);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not open serial port: " + e.Message);
        }
    }

    void Update()
    {
        if (serialPort == null || !serialPort.IsOpen || serialPort.BytesToRead <= 0)
            return;

        try
        {
            string line = serialPort.ReadLine().Trim();

            if (string.IsNullOrEmpty(line))
                return;

            // Ignore calibration messages
            if (line == "CALIBRATED")
            {
                Debug.Log("recalibrated");
                return;
            }

            string[] values = line.Split(',');

            if (values.Length >= 6)
            {
                 if (int.TryParse(values[0], out int parsedPot) &&
                    float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedPitch) &&
                    float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedRoll) &&
                    float.TryParse(values[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedGyro) &&
                    float.TryParse(values[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedAccel) &&
                    int.TryParse(values[5], out int parsedActionButton))
                {
                    if (!ignorePot)
                    {
                        rawPot = parsedPot;
                        sensorValue = Mathf.Clamp01(rawPot / 1023f);
                    }
                    else
                    {
                        rawPot = 0;
                        sensorValue = 0f;
                    }

                    pitch = parsedPitch;
                    roll = parsedRoll;
                    gyroY = parsedGyro;
                    accelZ = parsedAccel;
                    actionButton = parsedActionButton;
                }
            }
        }
        catch (TimeoutException)
        {
        }
        catch (System.Exception e)
        {
            Debug.Log("Serial error: " + e.Message);
        }
    }

    string FindPort()
    {
        var ports = SerialPort.GetPortNames();
        Debug.Log("Ports found: " + string.Join(", ", ports));

        if (Application.platform == RuntimePlatform.WindowsEditor ||
            Application.platform == RuntimePlatform.WindowsPlayer)
        {
            var winPreferred = ports
                .Where(p => p.StartsWith("COM", System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Length)
                .ThenBy(p => p)
                .FirstOrDefault();

            return winPreferred ?? ports.FirstOrDefault();
        }

        if (Application.platform == RuntimePlatform.OSXEditor ||
            Application.platform == RuntimePlatform.OSXPlayer)
        {
            var macPreferred = ports.FirstOrDefault(p =>
                p.Contains("usbmodem", System.StringComparison.OrdinalIgnoreCase) ||
                p.Contains("usbserial", System.StringComparison.OrdinalIgnoreCase) ||
                p.Contains("wchusbserial", System.StringComparison.OrdinalIgnoreCase));

            return macPreferred ?? ports.FirstOrDefault();
        }

        if (Application.platform == RuntimePlatform.LinuxEditor ||
            Application.platform == RuntimePlatform.LinuxPlayer)
        {
            var linuxPreferred = ports.FirstOrDefault(p =>
                p.Contains("ttyACM", System.StringComparison.OrdinalIgnoreCase) ||
                p.Contains("ttyUSB", System.StringComparison.OrdinalIgnoreCase));

            return linuxPreferred ?? ports.FirstOrDefault();
        }

        return ports.FirstOrDefault();
    }

    void OnApplicationQuit()
    {
        if (serialPort != null && serialPort.IsOpen)
            serialPort.Close();
    }

    public bool TryGetControlState(out SpatulaControlState state)
    {
        state = default;

        // Require an open serial stream before exposing control input.
        if (serialPort == null || !serialPort.IsOpen)
            return false;

        float currentRoll = invertRoll ? -roll : roll;

        debugGyroY = gyroY;
        debugRoll = currentRoll;

        state.PotValue = Mathf.Clamp01(sensorValue);
        state.HorizontalInput = Mathf.Abs(currentRoll) > rollDeadzone ? currentRoll : 0f;
        float normalizedPitch = Mathf.InverseLerp(minPitchInput, maxPitchInput, pitch);
        state.PitchNormalized = Mathf.Clamp01(normalizedPitch);


        bool currentActionButtonHeld = actionButton == 1;

        state.LockPressed = Input.GetKeyDown(lockKey) || (currentActionButtonHeld && !lastActionButtonHeld);
        state.LockHeld = Input.GetKey(lockKey) || currentActionButtonHeld;
        state.LockReleased = Input.GetKeyUp(lockKey) || (!currentActionButtonHeld && lastActionButtonHeld);

        lastActionButtonHeld = currentActionButtonHeld;

        bool isFlickingUp = gyroY >= gyroYThreshold;
        bool rollOK = Mathf.Abs(currentRoll) <= rollLimit;
        bool cooldownOK = (Time.time - lastFlipTime) >= cooldown;

        if (isFlickingUp && rollOK && cooldownOK)
        {
            lastFlipTime = Time.time;
            float strength = Mathf.Clamp(gyroY / gyroYThreshold, 1f, 2.5f);
            state.FlipTriggered = true;
            state.SnapRequested = true;
            state.FlipStrength = strength;
        }

        return true;
    }
}