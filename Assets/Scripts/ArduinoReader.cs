using UnityEngine;
using System.IO.Ports;
using System.Threading;
using System.Linq;

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

    [Header("Gyro Data")]
    public float pitch = 0f;
    public float roll = 0f;

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

            string[] values = line.Split(',');

            if (values.Length == 3)
            {
                if (int.TryParse(values[0], out int parsedPot) &&
                    float.TryParse(values[1], out float parsedPitch) &&
                    float.TryParse(values[2], out float parsedRoll))
                {
                    rawPot = parsedPot;

                    // normalize pot
                    sensorValue = Mathf.Clamp01(rawPot / 1023f);

                    pitch = parsedPitch;
                    roll = parsedRoll;
                }
            }
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

        // Windows: COM ports
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

        // macOS: /dev/cu.usbmodem*, /dev/cu.usbserial*, /dev/cu.wchusbserial*
        if (Application.platform == RuntimePlatform.OSXEditor ||
            Application.platform == RuntimePlatform.OSXPlayer)
        {
            var macPreferred = ports.FirstOrDefault(p =>
                p.Contains("usbmodem", System.StringComparison.OrdinalIgnoreCase) ||
                p.Contains("usbserial", System.StringComparison.OrdinalIgnoreCase) ||
                p.Contains("wchusbserial", System.StringComparison.OrdinalIgnoreCase));

            return macPreferred ?? ports.FirstOrDefault();
        }

        // Linux: common USB serial names
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