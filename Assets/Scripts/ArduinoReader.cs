using UnityEngine;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Globalization;
using System;
using System.Threading;

public class ArduinoReader : MonoBehaviour, ISpatulaInput, ISpatulaInputBackgroundActivity
{
    [Header("Serial Port Settings")]
    public string portName = "";
    public int baudRate = 115200;
    [Tooltip("How often to retry opening a serial connection when unavailable")]
    public float reconnectInterval = 1.5f;
    [Tooltip("Consider the stream stale if no valid packet arrives in this many seconds")]
    public float staleDataTimeout = 1.2f;
    [Tooltip("Reconnect after this many consecutive malformed packets")]
    public int maxConsecutiveGarbagePackets = 12;
    [Tooltip("Max time to wait for the first valid packet after opening the port")]
    public float firstPacketTimeout = 6f;
    [Tooltip("Throttle interval in seconds for EnsureConnected debug logs")]
    public float ensureConnectedLogInterval = 2f;

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
    private float lastEnsureConnectedLogTime = -999f;
    private string lastEnsureConnectedReason = string.Empty;
    private float lastPitchSample;
    private float lastPitchSampleTime = -1f;
    private float latestPitchDelta;
    private float filteredPitchVelocity;

    public bool IsBackgroundActivityEnabled { get; set; } = true;

    void Start()
    {
        TryOpenSerial();
    }

    void Update()
    {
        if (!IsBackgroundActivityEnabled)
        {
            UpdateConnectionStateDebug();
            return;
        }

        UpdateConnectionStateDebug();

        if (!EnsureConnected())
            return;

        try
        {
            if (serialPort == null || !serialPort.IsOpen)
                return;

            if (serialPort.BytesToRead <= 0)
            {
                if (hasReceivedValidPacketSinceOpen)
                {
                    if (Time.time - lastValidPacketTime >= staleDataTimeout)
                        ForceReconnect("stale serial stream");
                }
                else if (Time.time - lastOpenTime >= firstPacketTimeout)
                {
                    ForceReconnect("no initial serial data");
                }

                return;
            }

            string line = serialPort.ReadLine().Trim();

            if (string.IsNullOrEmpty(line))
                return;

            // Ignore calibration messages
            if (line == "CALIBRATED")
            {
                Debug.Log("recalibrated");
                return;
            }

            if (!TryParsePacket(line, out ParsedPacket packet))
            {
                RegisterGarbagePacket(line);
                return;
            }

            ApplyPacket(packet);
        }
        catch (TimeoutException)
        {
        }
        catch (System.Exception e)
        {
            LogSerialError("Serial read error. Reconnecting.", e);
            ForceReconnect("serial read error");
        }
    }

    bool EnsureConnected()
    {
        if (isCloseInProgress)
        {
            LogEnsureConnectedBlocked("serial close in progress");
            return false;
        }

        if (serialPort != null && serialPort.IsOpen)
            return true;

        if (Time.time < nextReconnectAttemptTime)
        {
            float remainingSeconds = Mathf.Max(0f, nextReconnectAttemptTime - Time.time);
            LogEnsureConnectedBlocked("waiting for reconnect timer", "remaining " + remainingSeconds.ToString("F2", CultureInfo.InvariantCulture) + "s");
            return false;
        }

        portName = FindPort();
        bool opened = TryOpenSerial();
        if (!opened)
            LogEnsureConnectedBlocked("open attempt failed");

        return opened;
    }

    void LogEnsureConnectedBlocked(string reason, string detail = null)
    {
        float interval = Mathf.Max(0.1f, ensureConnectedLogInterval);
        bool reasonChanged = !string.Equals(reason, lastEnsureConnectedReason, StringComparison.Ordinal);

        if (!reasonChanged && (Time.time - lastEnsureConnectedLogTime) < interval)
            return;

        lastEnsureConnectedLogTime = Time.time;
        lastEnsureConnectedReason = reason;

        if (string.IsNullOrEmpty(detail))
        {
            Debug.LogWarning("[ArduinoReader] EnsureConnected blocked: " + reason);
            return;
        }

        Debug.LogWarning("[ArduinoReader] EnsureConnected blocked: " + reason + " (" + detail + ")");
    }

    bool TryOpenSerial()
    {
        if (isCloseInProgress)
        {
            ScheduleReconnect();
            return false;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(portName) || !SerialPort.GetPortNames().Contains(portName))
                portName = FindPort();

            if (string.IsNullOrWhiteSpace(portName))
            {
                Debug.LogWarning("No serial ports found.");
                ScheduleReconnect();
                return false;
            }

            if (serialPort != null)
            {
                if (serialPort.IsOpen)
                {
                    CloseSerialAsync();
                    ScheduleReconnect();
                    return false;
                }

                try
                {
                    serialPort.Dispose();
                }
                catch
                {
                }

                serialPort = null;
            }
            serialPort = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 500
            };

            serialPort.Open();
            serialPort.DiscardInBuffer();
            consecutiveGarbagePackets = 0;
            hasReceivedValidPacketSinceOpen = false;
            lastOpenTime = Time.time;
            lastValidPacketTime = Time.time;
            Debug.Log("Opened: " + portName);
            return true;
        }
        catch (System.Exception e)
        {
            LogSerialError(BuildOpenSerialErrorMessage(e), e);
            ScheduleReconnect();
            return false;
        }
    }

    void LogSerialError(string message, Exception exception = null)
    {
        string prefix = "[ArduinoReader] ";
        if (exception == null)
        {
            Debug.LogError(prefix + message);
            return;
        }

        Debug.LogError(prefix + message + "\n" + exception);
    }

    string BuildOpenSerialErrorMessage(Exception exception)
    {
        string targetPort = string.IsNullOrWhiteSpace(portName) ? "<auto>" : portName;
        string message = "Could not open serial port '" + targetPort + "' at " + baudRate + " baud. Reconnect scheduled.";

        bool isLinux = Application.platform == RuntimePlatform.LinuxEditor ||
                       Application.platform == RuntimePlatform.LinuxPlayer;

        if (isLinux &&
            exception is IOException &&
            exception.Message.IndexOf("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            message += " Linux permissions hint: add your user to the serial device group (often 'uucp' or 'dialout') and relogin.";
        }

        return message;
    }

    void ScheduleReconnect()
    {
        nextReconnectAttemptTime = Time.time + Mathf.Max(0.1f, reconnectInterval);
    }

    void ForceReconnect(string reason)
    {
        Debug.LogWarning("Reconnecting serial: " + reason);
        CloseSerialAsync();
        ScheduleReconnect();
    }

    void CloseSerialAsync()
    {
        if (serialPort == null)
            return;

        SerialPort portToClose = serialPort;
        serialPort = null;
        isCloseInProgress = true;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                if (portToClose.IsOpen)
                    portToClose.Close();
            }
            catch
            {
            }
            finally
            {
                try
                {
                    portToClose.Dispose();
                }
                catch
                {
                }

                isCloseInProgress = false;
            }
        });
    }

    [ContextMenu("Force Connect")]
    public void ForceConnectNow()
    {
        if (isCloseInProgress)
        {
            Debug.LogWarning("Cannot force connect while serial close is in progress.");
            return;
        }

        CloseSerialBlocking();
        nextReconnectAttemptTime = 0f;
        TryOpenSerial();
    }

    void CloseSerialBlocking()
    {
        if (serialPort == null)
            return;

        SerialPort portToClose = serialPort;
        serialPort = null;

        try
        {
            if (portToClose.IsOpen)
                portToClose.Close();
        }
        catch
        {
        }

        try
        {
            portToClose.Dispose();
        }
        catch
        {
        }

        isCloseInProgress = false;
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

        if (lastPitchSampleTime < 0f)
        {
            lastPitchSample = pitch;
            lastPitchSampleTime = Time.time;
            latestPitchDelta = 0f;
            filteredPitchVelocity = 0f;
            return;
        }

        float sampleDeltaTime = Mathf.Max(0.0001f, Time.time - lastPitchSampleTime);
        latestPitchDelta = pitch - lastPitchSample;
        float rawPitchVelocity = latestPitchDelta / sampleDeltaTime;
        float smoothing = Mathf.Clamp01(pitchVelocitySmoothing);
        filteredPitchVelocity = Mathf.Lerp(filteredPitchVelocity, rawPitchVelocity, smoothing);

        lastPitchSample = pitch;
        lastPitchSampleTime = Time.time;
    }

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

    void UpdateConnectionStateDebug()
    {
        isConnected = serialPort != null && serialPort.IsOpen;
        secondsSinceLastValidPacket = lastValidPacketTime < 0f ? float.PositiveInfinity : Time.time - lastValidPacketTime;
    }

    static bool IsFinite(float value)
    {
        return !(float.IsNaN(value) || float.IsInfinity(value));
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

    public bool TryGetControlState(out SpatulaControlState state)
    {
        state = default;

        // Require an open serial stream before exposing control input.
        if (serialPort == null || !serialPort.IsOpen)
            return false;

        if (Time.time - lastValidPacketTime > staleDataTimeout)
            return false;

        float currentRoll = invertRoll ? -roll : roll;
        float pitchVelocity = filteredPitchVelocity;

        debugGyroY = gyroY;
        debugPitch = pitch;
        debugPitchDelta = latestPitchDelta;
        debugPitchVelocity = pitchVelocity;
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

        bool flipArmHeld = currentActionButtonHeld || keyboardHeld;
        bool isFlickingUp = pitchVelocity >= pitchFlipVelocityThreshold;
        bool pitchDeltaOK = Mathf.Abs(latestPitchDelta) >= pitchDeltaThreshold;
        bool rollOK = Mathf.Abs(currentRoll) <= rollLimit;
        bool cooldownOK = (Time.time - lastFlipTime) >= cooldown;

        if (flipArmHeld && isFlickingUp && pitchDeltaOK && rollOK && cooldownOK)
        {
            lastFlipTime = Time.time;
            float strength = Mathf.Clamp(pitchVelocity / Mathf.Max(0.1f, pitchFlipVelocityThreshold), 1f, 2.5f);
            state.FlipTriggered = true;
            state.SnapRequested = true;
            state.FlipStrength = strength;
        }

        return true;
    }

    [ContextMenu("Refind Port")]
    public void ForceRefindPortNow()
    {
        if (isCloseInProgress)
        {
            Debug.LogWarning("Cannot refind port while serial close is in progress.");
            return;
        }

        portName = FindPort();
        Debug.Log("Refind Port: " + (string.IsNullOrEmpty(portName) ? "No port found" : portName));
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
}