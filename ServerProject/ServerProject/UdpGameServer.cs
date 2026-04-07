using System;
using System.Collections.Generic;
using System.Globalization;
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

        // 仅通过坐标做合理性检查
        private const double MaxMoveSpeedPerSecond = 3; // 允许的最大位移速度
        private const double PositionTolerance = 0.75;     // 额外容差，避免正常抖动误判
        private const double HardSnapDistance = 5.0;       // 超过这个值直接判定为重大异常，拉回原坐标

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
            ClientSession session;

            lock (_lock)
            {
                if (!_clients.ContainsKey(key))
                {
                    _clients[key] = new ClientSession(remoteEndPoint);
                    Console.WriteLine($"Client connected: {key}");
                }

                session = _clients[key];
                session.LastSeenUtc = DateTime.UtcNow;
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
                return;
            }

            if (cmd == "POS")
            {
                HandlePositionReport(session, parts);
            }
        }

        private void HandlePositionReport(ClientSession session, string[] parts)
        {
            if (parts.Length < 4) return;

            if (!TryParseFloat(parts[1], out float x) ||
                !TryParseFloat(parts[2], out float y) ||
                !TryParseFloat(parts[3], out float z))
            {
                return;
            }

            DateTime now = DateTime.UtcNow;

            lock (_lock)
            {
                if (!session.HasAcceptedPosition)
                {
                    session.SetAcceptedPosition(x, y, z, now);
                    Console.WriteLine($"Init position accepted: {session.EndPoint} -> ({x:F2}, {y:F2}, {z:F2})");
                    return;
                }

                double dt = (now - session.LastAcceptedPositionUtc).TotalSeconds;
                if (dt < 0.001)
                {
                    dt = 0.001;
                }

                double dx = x - session.LastAcceptedX;
                double dy = y - session.LastAcceptedY;
                double dz = z - session.LastAcceptedZ;
                double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                double allowedDistance = (MaxMoveSpeedPerSecond * dt) + PositionTolerance;

                // 重大异常：直接拉回服务端最后认可的原坐标
                if (distance > HardSnapDistance || distance > allowedDistance * 2.0)
                {
                    string correctMsg = string.Format(
                        CultureInfo.InvariantCulture,
                        "CORRECT|{0:F3}|{1:F3}|{2:F3}",
                        session.LastAcceptedX,
                        session.LastAcceptedY,
                        session.LastAcceptedZ);

                    Send(session.EndPoint, correctMsg);

                    Console.WriteLine(
                        $"Hard correction: {session.EndPoint}, reported=({x:F2}, {y:F2}, {z:F2}), accepted=({session.LastAcceptedX:F2}, {session.LastAcceptedY:F2}, {session.LastAcceptedZ:F2}), dist={distance:F2}, allowed={allowedDistance:F2}");
                    return;
                }

                // 正常范围内，接受新坐标作为新的服务端认可位置
                if (distance <= allowedDistance)
                {
                    session.SetAcceptedPosition(x, y, z, now);
                    return;
                }

                // 轻微可疑：先不更新，也先不强制拉回，避免手感过差
                Console.WriteLine(
                    $"Suspicious position ignored: {session.EndPoint}, reported=({x:F2}, {y:F2}, {z:F2}), accepted=({session.LastAcceptedX:F2}, {session.LastAcceptedY:F2}, {session.LastAcceptedZ:F2}), dist={distance:F2}, allowed={allowedDistance:F2}");
            }
        }

        private bool TryParseFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
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
