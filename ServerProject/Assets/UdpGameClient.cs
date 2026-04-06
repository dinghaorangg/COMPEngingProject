using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UdpGameClient : MonoBehaviour
{
    [Header("Server")]
    public string serverIp = "127.0.0.1";
    public int serverPort = 7777;

    [Header("Ping Settings")]
    public float pingInterval = 1.0f;
    public float disconnectTimeout = 5.0f;

    public int CurrentPingMs { get; private set; }
    public bool IsConnected { get; private set; }

    private UdpClient udpClient;
    private IPEndPoint serverEndPoint;
    private Thread receiveThread;
    private volatile bool running;

    private float pingTimer;
    private DateTime lastReceiveTimeUtc;

    void Start()
    {
        StartClient();
    }

    void Update()
    {
        if (!running) return;

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
        UdpClient client = new UdpClient();
        try
        {
            running = true;
            byte[] data = Encoding.UTF8.GetBytes("Client: Hello World UDP");
            client.Send(data, data.Length, serverIp, serverPort);
            Debug.Log("UDP Msg Sent");

            client.Client.ReceiveTimeout = 2000;

            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] receivedData = client.Receive(ref remote);
            string response = Encoding.UTF8.GetString(receivedData);
            Debug.Log("Server replied: " + response);
        }
        catch (System.Exception e)
        {
            Debug.LogError("UDP Error:" + e.Message);
        }
        finally
        {
            client.Close();
        }
        // try
        // {
        //     serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
        //     udpClient = new UdpClient(0);
        //     udpClient.Connect(serverEndPoint);

        //     running = true;
        //     lastReceiveTimeUtc = DateTime.UtcNow;

        //     receiveThread = new Thread(ReceiveLoop);
        //     receiveThread.IsBackground = true;
        //     receiveThread.Start();

        //     Debug.Log("UDP client started.");
        // }
        // catch (Exception ex)
        // {
        //     Debug.LogError($"StartClient Error: {ex.Message}");
        // }
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