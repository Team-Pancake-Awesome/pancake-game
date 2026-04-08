using UnityEngine;
using System.IO.Ports;
using System.Linq;
using System.Globalization;
using System;
using System.Threading;
using System.IO;

public class ArduinoReader : MonoBehaviour, ISpatulaInput, ISpatulaInputBackgroundActivity
{
    [Header("Serial Port Settings")]
    public string portName = "";
    public int baudRate = 115200;
    [Tooltip("How often to retry opening a serial connection when unavailable, in seconds")]
    public float reconnectInterval = 1.5f;
    [Tooltip("Consider the stream stale if no valid packet arrives in this many seconds")]
    public float staleDataTimeout = 1.2f;
    [Tooltip("Reconnect after this many consecutive malformed packets")]
    public int maxConsecutiveGarbagePackets = 12;
    [Tooltip("Max time to wait for the first valid packet after opening the port, in seconds")]
    public float firstPacketTimeout = 6f;

    private SerialPort serialPort;

    [Header("Pot Data")]
    [Range(0f, 1f)]
    public float sensorValue = 0f;
    public int rawPot = 0;

    [Header("Debug")]
    [Tooltip("Ignore potentiometer input during play for debugging.")]
    public bool ignorePot = false;
    [Tooltip("Allows keyboard lock input as a debug fallback while using Arduino")]
    public bool enableKeyboardLockFallback = false;
    public bool logEvents = true;

    [Header("Gyro Data")]
    public float pitch = 0f;
    public float roll = 0f;
    public float gyroY = 0f; //
    public float accelZ = 0f; // captures upward thrust
    public int actionButton = 0; //space 

    [Header("Arduino Controls")]
    public KeyCode lockKey = KeyCode.Space; // TODO remove
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
    public bool isConnected;
    public float secondsSinceLastValidPacket;

    private float lastFlipTime = -999f;
    private bool lastActionButtonHeld;
    private float nextReconnectAttemptTime;
    private float lastValidPacketTime = -999f;
    private int consecutiveGarbagePackets;
    private volatile bool isCloseInProgress;
    private int malformedLogCounter;
    private bool hasReceivedValidPacketSinceOpen;
    private float lastOpenTime = -999f;

    // What's this used for?
    public bool IsBackgroundActivityEnabled { get; set; } = true;

    #region Port Opening Methods

    void Start()
    {
        TryOpenSerial();
    }

    bool TryOpenSerial()
    {
        if (isCloseInProgress)
        {
            // In theory, this should never happen when we don't want the reader to connect, right?
            ScheduleReconnect();
            return false;
        }

        if (logEvents) Debug.Log("Starting serial port opening sequence...");

        // If no port is provided, try to find one
        if (string.IsNullOrWhiteSpace(portName) || !SerialPort.GetPortNames().Contains(portName))
        {
            if (logEvents) Debug.LogWarning("No port provided. Attempting to find one...");
            portName = FindPort();
        }

        // If we still couldn't find a port, schedule a reconnect
        if (string.IsNullOrWhiteSpace(portName))
        {
            if (logEvents) Debug.LogWarning("No serial ports found. Scheduling a reconnect...");
            ScheduleReconnect();
            return false;
        }

        // It looks like this force closes a port that's open if we try to reconnect, I'm not sure this is a good idea
        // Let's table this for now...
        if (serialPort != null)
        {
            if (serialPort.IsOpen)
            {
                CloseSerialAsync();
                ScheduleReconnect();
                return false;
            }

            serialPort = null;
        }

        // Create new SerialPort
        // Potential problem, some methods check that this is null. If for some reason 
        // the port can't be opened, this is never set to null after being given a value
        serialPort = new SerialPort(portName, baudRate)
        {
            ReadTimeout = 500   // Half a second. Is this too short?
        };

        // Attempt to open the port
        try
        {
            serialPort.Open();
        }
        catch (System.Exception e)
        {
            if (logEvents) Debug.LogWarning("Could not open serial port: " + e.Message);
            ScheduleReconnect();
            return false;
        }
        if (logEvents) Debug.Log("Opened: " + portName);

        // Discard any garbage data in the receive buffer
        serialPort.DiscardInBuffer();

        // Set beginning state
        consecutiveGarbagePackets = 0;
        hasReceivedValidPacketSinceOpen = false;
        lastOpenTime = Time.time;
        lastValidPacketTime = Time.time;

        return true;
    }

    string FindPort()
    {
        var ports = SerialPort.GetPortNames();
        if (logEvents) Debug.Log("Ports found: " + string.Join(", ", ports));

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

    void ScheduleReconnect()
    {
        nextReconnectAttemptTime = Time.time + Mathf.Max(0.1f, reconnectInterval);
        if (logEvents) Debug.Log("Will try reconnecting at: " + nextReconnectAttemptTime);
    }

    #endregion

    #region Port Stabilizing Methods

    void Update()
    {
        // This always gets called
        UpdateConnectionStateDebug();

        // If background activity is not enabled or there is no connection, skip the next logic
        if (!IsBackgroundActivityEnabled || !EnsureConnected())
            return;

        // Again, this is a long try block, it's gonna be hard to determine what the problem is
        // if it has to try all of this logic 
        try
        {
            // Checking this again, but this is already checked in EnsureConnected...
            if (serialPort == null || !serialPort.IsOpen)
                return;

            // If there are no bytes to read...
            if (serialPort.BytesToRead <= 0)
            {
                // Check that we've received good data before, if we have the stream could be stale
                if (hasReceivedValidPacketSinceOpen)
                {
                    if (Time.time - lastValidPacketTime >= staleDataTimeout)
                        ForceReconnect("stale serial stream");
                }
                // If we never received good data, then maybe reconnect... 
                else if (Time.time - lastOpenTime >= firstPacketTimeout)
                {
                    ForceReconnect("no initial serial data");
                }

                // Question: Do we need to check both of these things? Surely we just need one
                // check here, which is whether the stream is stale

                return;
            }

            string line = serialPort.ReadLine().Trim();

            if (string.IsNullOrEmpty(line))
                return;

            // Ignore calibration messages
            if (line == "CALIBRATED")
            {
                if (logEvents) Debug.Log("recalibrated");
                return;
            }

            // Attempt to parse the packet
            if (TryParsePacket(line, out ParsedPacket packet))
                ApplyPacket(packet);
            else
                RegisterGarbagePacket(line);
        }
        catch (TimeoutException)
        {
            // Nothing for now I guess...
        }
        catch (System.Exception e)
        {
            ForceReconnect("serial read error: " + e.Message);
        }
    }

    /// <summary>
    /// <para>
    /// Checks to see whether the Arduino is still connected. 
    /// </para>
    /// <para>
    /// If the stored serial port is not null and the port is open,
    /// then a connection is likely. If serial port is being closed or is waiting 
    /// for a scheduled reconnect, then there is likely no connection
    /// </para>
    /// This method will try to open the serial port when a scheduled connection time is reached.
    /// </summary>
    /// <returns></returns>
    bool EnsureConnected()
    {
        if (isCloseInProgress || Time.time < nextReconnectAttemptTime)
            return false;

        if (serialPort != null && serialPort.IsOpen)
            return true;

        // This shouldn't be here, this is logic for whoever is calling this method.
        return TryOpenSerial();
    }

    void ForceReconnect(string reason)
    {
        Debug.LogWarning("Reconnecting serial: " + reason);
        CloseSerialAsync();
        ScheduleReconnect();
    }

    void UpdateConnectionStateDebug()
    {
        isConnected = serialPort != null && serialPort.IsOpen;
        secondsSinceLastValidPacket = lastValidPacketTime < 0f ? float.PositiveInfinity : Time.time - lastValidPacketTime;
    }

    #endregion

    #region Port Closing Methods

    void CloseSerialAsync()
    {
        if (serialPort == null)
            return;

        SerialPort portToClose = serialPort;
        serialPort = null;
        isCloseInProgress = true;   // Do we need to lock this variable?

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                if (portToClose.IsOpen)
                    portToClose.Close();
            }
            catch (IOException e)
            {
                if (logEvents) Debug.LogError($"Could not close serial port: {e.Message}");
            }
            finally
            {
                // I removed redundant code here-- SerialPort.Close() just calls Dispose(), like that's all it does lol

                isCloseInProgress = false;
            }
        });
    }

    void OnApplicationQuit()
    {
        CloseSerialAsync();
    }

    void OnDisable()
    {
        CloseSerialAsync();
    }

    void OnDestroy()
    {
        CloseSerialAsync();
    }

    #endregion

    #region Packet Data Methods

    bool TryParsePacket(string line, out ParsedPacket packet)
    {
        packet = default;

        string[] values = line.Split(',');
        if (values.Length < 6)
            return false;

        if (!int.TryParse(values[0], out int parsedPot))
            return false;

        if (!float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedPitch))
            return false;

        if (!float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedRoll))
            return false;

        if (!float.TryParse(values[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedGyro))
            return false;

        if (!float.TryParse(values[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedAccel))
            return false;

        if (!int.TryParse(values[5], out int parsedActionButton))
            return false;

        if (!IsFinite(parsedPitch) || !IsFinite(parsedRoll) || !IsFinite(parsedGyro) || !IsFinite(parsedAccel))
            return false;

        if (parsedPot < 0 || parsedPot > 1023)
            return false;

        if (parsedActionButton != 0 && parsedActionButton != 1)
            return false;

        packet = new ParsedPacket
        {
            Pot = parsedPot,
            Pitch = parsedPitch,
            Roll = parsedRoll,
            GyroY = parsedGyro,
            AccelZ = parsedAccel,
            ActionButton = parsedActionButton
        };

        return true;
    }

    void ApplyPacket(ParsedPacket packet)
    {
        consecutiveGarbagePackets = 0;
        hasReceivedValidPacketSinceOpen = true;
        lastValidPacketTime = Time.time;

        if (!ignorePot)
        {
            rawPot = packet.Pot;
            sensorValue = Mathf.Clamp01(rawPot / 1023f);
        }
        else
        {
            rawPot = 0;
            sensorValue = 0f;
        }

        pitch = packet.Pitch;
        roll = packet.Roll;
        gyroY = packet.GyroY;
        accelZ = packet.AccelZ;
        actionButton = packet.ActionButton;
    }

    void RegisterGarbagePacket(string line)
    {
        if (!hasReceivedValidPacketSinceOpen && Time.time - lastOpenTime < firstPacketTimeout)
            return;

        consecutiveGarbagePackets++;
        if (consecutiveGarbagePackets >= maxConsecutiveGarbagePackets)
        {
            ForceReconnect("too many malformed packets");
            return;
        }

        // Throttle warning volume to avoid flooding Editor logs on noisy serial lines.
        malformedLogCounter++;
        if (malformedLogCounter % 15 == 0)
            Debug.LogWarning("Malformed serial packet sample: " + line);
    }

    // TODO: float.IsFinite is already a built-in function? Can this be removed?
    static bool IsFinite(float value)
    {
        return !(float.IsNaN(value) || float.IsInfinity(value));
    }

    public bool TryGetControlState(out SpatulaControlState state)
    {
        state = default;

        // Require an open serial stream before exposing control input.
        if (serialPort == null || !serialPort.IsOpen)
            return false;

        if (Time.time - lastValidPacketTime > staleDataTimeout)
            return false;

        float currentRoll = invertRoll ? -roll : roll;

        debugGyroY = gyroY;
        debugRoll = currentRoll;

        state.PotValue = Mathf.Clamp01(sensorValue);
        state.HorizontalInput = Mathf.Abs(currentRoll) > rollDeadzone ? currentRoll : 0f;
        float normalizedPitch = Mathf.InverseLerp(minPitchInput, maxPitchInput, pitch);
        state.PitchNormalized = Mathf.Clamp01(normalizedPitch);


        bool currentActionButtonHeld = actionButton == 1;

        bool keyboardPressed = enableKeyboardLockFallback && Input.GetKeyDown(lockKey);
        bool keyboardHeld = enableKeyboardLockFallback && Input.GetKey(lockKey);
        bool keyboardReleased = enableKeyboardLockFallback && Input.GetKeyUp(lockKey);

        state.LockPressed = keyboardPressed || (currentActionButtonHeld && !lastActionButtonHeld);
        state.LockHeld = keyboardHeld || currentActionButtonHeld;
        state.LockReleased = keyboardReleased || (!currentActionButtonHeld && lastActionButtonHeld);

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

    struct ParsedPacket
    {
        public int Pot;
        public float Pitch;
        public float Roll;
        public float GyroY;
        public float AccelZ;
        public int ActionButton;
    }

    #endregion
}