using UnityEngine;
using System.IO.Ports;
using System.Linq;
using System.Globalization;
using System;

public class ArduinoReader : MonoBehaviour
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

            if (values.Length >= 5)
            {
                 if (int.TryParse(values[0], out int parsedPot) &&
                    float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedPitch) &&
                    float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedRoll) &&
                    float.TryParse(values[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedGyro) &&
                    float.TryParse(values[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedAccel))
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
}