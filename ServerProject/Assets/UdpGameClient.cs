using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UdpGameClient : MonoBehaviour
{
    [Header("Server")]
    public string serverIp = "175.178.240.22";
    public int serverPort = 7777;

    [Header("Ping Settings")]
    public float pingInterval = 1.0f;
    public float disconnectTimeout = 5.0f;

    [Header("Position Sync")]
    public float positionSendInterval = 0.1f;

    public int CurrentPingMs { get; private set; }
    public bool IsConnected { get; private set; }

    private UdpClient udpClient;
    private IPEndPoint serverEndPoint;
    private Thread receiveThread;
    private volatile bool running;

    private float pingTimer;
    private float positionTimer;
    private DateTime lastReceiveTimeUtc;

    private readonly object correctionLock = new object();
    private bool hasPendingCorrection;
    private Vector3 pendingCorrectionPosition;
    private volatile bool networkBlocked;

    void Start()
    {
        StartClient();
    }

    void Update()
    {
        if (!running) return;

        networkBlocked = Input.GetKey(KeyCode.P);

        pingTimer += Time.deltaTime;
        if (pingTimer >= pingInterval)
        {
                pingTimer = 0f;
                SendPing();
        }

        if (IsConnected)
        {
            double silence = (DateTime.UtcNow - lastReceiveTimeUtc).TotalSeconds;
            if (silence > disconnectTimeout)
            {
                IsConnected = false;
                Debug.LogWarning("Disconnected: timeout");
            }
        }
    }

    public void StartClient()
    {
        try
        {
            serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            udpClient = new UdpClient(0);
            udpClient.Connect(serverEndPoint);

            running = true;
            lastReceiveTimeUtc = DateTime.UtcNow;

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Debug.Log("UDP client started.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"StartClient Error: {ex.Message}");
        }
    }

    public void StopClient()
    {
        running = false;

        try
        {
            udpClient?.Close();
        }
        catch { }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(100);
        }
    }

    private void SendPing()
    {
        if (networkBlocked)
        {
            return;
        }
        try
        {
            long clientTicks = DateTime.UtcNow.Ticks;
            string msg = $"PING|{clientTicks}";
            byte[] data = Encoding.UTF8.GetBytes(msg);
            udpClient.Send(data, data.Length);
        }
        catch (Exception ex)
        {
            Debug.LogError($"SendPing Error: {ex.Message}");
        }
    }

    public void TickPositionSync(Vector3 position)
    {
        if (networkBlocked)
        {
            return;
        }
        if (!running || udpClient == null)
        {
            return;
        }

        positionTimer += Time.deltaTime;
        if (positionTimer < positionSendInterval)
        {
            return;
        }

        positionTimer = 0f;

        try
        {
            string msg = string.Format(
                CultureInfo.InvariantCulture,
                "POS|{0:F3}|{1:F3}|{2:F3}",
                position.x,
                position.y,
                position.z);

            byte[] data = Encoding.UTF8.GetBytes(msg);
            udpClient.Send(data, data.Length);
        }
        catch (Exception ex)
        {
            Debug.LogError($"TickPositionSync Error: {ex.Message}");
        }
    }

    public bool TryConsumeCorrection(out Vector3 correctedPosition)
    {
        lock (correctionLock)
        {
            if (hasPendingCorrection)
            {
                correctedPosition = pendingCorrectionPosition;
                hasPendingCorrection = false;
                return true;
            }
        }

        correctedPosition = default;
        return false;
    }

    private void ReceiveLoop()
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remote);
                string msg = Encoding.UTF8.GetString(data);
                HandleMessage(msg);
            }
            catch (SocketException)
            {
                if (!running) break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"ReceiveLoop Error: {ex.Message}");
            }
        }
    }

    private void HandleMessage(string msg)
    {
        if (networkBlocked)
        {
            return;
        }
        string[] parts = msg.Split('|');
        if (parts.Length == 0) return;

        string cmd = parts[0];

        if (cmd == "PONG" && parts.Length >= 2)
        {
            if (long.TryParse(parts[1], out long clientTicks))
            {
                DateTime sentTime = new DateTime(clientTicks, DateTimeKind.Utc);
                TimeSpan rtt = DateTime.UtcNow - sentTime;
                CurrentPingMs = Mathf.Max(0, (int)rtt.TotalMilliseconds);

                lastReceiveTimeUtc = DateTime.UtcNow;
                IsConnected = true;
            }
            return;
        }

        if (cmd == "CORRECT" && parts.Length >= 4)
        {
            if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                lock (correctionLock)
                {
                    pendingCorrectionPosition = new Vector3(x, y, z);
                    hasPendingCorrection = true;
                }

                lastReceiveTimeUtc = DateTime.UtcNow;
                IsConnected = true;
                Debug.Log($"Server correction received: ({x:F2}, {y:F2}, {z:F2})");
            }
        }
    }
  

    private void OnDestroy()
    {
        StopClient();
    }

    private void OnApplicationQuit()
    {
        StopClient();
    }
}
