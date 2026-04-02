using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DedicatedServer
{
    public class UdpGameServer
    {
        private readonly int _port;
        private UdpClient _udp;
        private Thread _receiveThread;
        private Thread _timeoutThread;
        private volatile bool _running;

        private readonly Dictionary<string, ClientSession> _clients = new Dictionary<string, ClientSession>();
        private readonly object _lock = new object();

        public UdpGameServer(int port)
        {
            _port = port;
        }

        public void Start()
        {
            _udp = new UdpClient(_port);
            _running = true;

            _receiveThread = new Thread(ReceiveLoop);
            _receiveThread.IsBackground = true;
            _receiveThread.Start();

            _timeoutThread = new Thread(CheckTimeoutLoop);
            _timeoutThread.IsBackground = true;
            _timeoutThread.Start();

            Console.WriteLine($"UDP server started on port {_port}");
        }

        public void Stop()
        {
            _running = false;

            try
            {
                _udp?.Close();
            }
            catch { }

            Console.WriteLine("Server stopped.");
        }

        private void ReceiveLoop()
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (_running)
            {
                try
                {
                    byte[] data = _udp.Receive(ref remoteEndPoint);
                    string msg = Encoding.UTF8.GetString(data);

                    HandleMessage(remoteEndPoint, msg);
                }
                catch (SocketException)
                {
                    if (!_running) break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ReceiveLoop Error: {ex.Message}");
                }
            }
        }

        private void HandleMessage(IPEndPoint remoteEndPoint, string msg)
        {
            string key = remoteEndPoint.ToString();

            lock (_lock)
            {
                if (!_clients.ContainsKey(key))
                {
                    _clients[key] = new ClientSession(remoteEndPoint);
                    Console.WriteLine($"Client connected: {key}");
                }

                _clients[key].LastSeenUtc = DateTime.UtcNow;
            }

            string[] parts = msg.Split('|');
            if (parts.Length == 0) return;

            string cmd = parts[0];

            if (cmd == "PING")
            {
                if (parts.Length >= 2)
                {
                    string clientTicks = parts[1];
                    string serverTicks = DateTime.UtcNow.Ticks.ToString();
                    string pong = $"PONG|{clientTicks}|{serverTicks}";
                    Send(remoteEndPoint, pong);
                }
            }
        }

        private void Send(IPEndPoint endPoint, string msg)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(msg);
            _udp.Send(bytes, bytes.Length, endPoint);
        }

        private void CheckTimeoutLoop()
        {
            while (_running)
            {
                try
                {
                    List<string> timeoutClients = new List<string>();

                    lock (_lock)
                    {
                        foreach (var kv in _clients)
                        {
                            double seconds = (DateTime.UtcNow - kv.Value.LastSeenUtc).TotalSeconds;
                            if (seconds > 5.0)
                            {
                                timeoutClients.Add(kv.Key);
                            }
                        }

                        foreach (string key in timeoutClients)
                        {
                            Console.WriteLine($"Client timeout: {key}");
                            _clients.Remove(key);
                        }
                    }

                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CheckTimeoutLoop Error: {ex.Message}");
                }
            }
        }
    }
}