using UnityEngine;
using System.IO.Ports;
using System.Threading;

public class GyroReader : MonoBehaviour
{
    [Header("Serial Port Settings")]
    public string portName = "COM4";
    public int baudRate = 115200;

    private SerialPort serialPort;

    [Header("Gyro Data")]
    public float pitch = 0f;
    public float roll = 0f;

    void Start()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = 500;
            serialPort.Open();
            Debug.Log("Gyro serial port opened. Waiting for Arduino to start...");

            Thread.Sleep(2000);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not open gyro serial port: " + e.Message);
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

            if (values.Length == 2)
            {
                if (float.TryParse(values[0], out float parsedPitch) &&
                    float.TryParse(values[1], out float parsedRoll))
                {
                    pitch = parsedPitch;
                    roll = parsedRoll;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.Log("Gyro serial error: " + e.Message);
        }
    }

    void OnApplicationQuit()
    {
        if (serialPort != null && serialPort.IsOpen)
            serialPort.Close();
    }
}