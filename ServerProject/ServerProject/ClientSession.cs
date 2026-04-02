using System;
using System.Net;

namespace DedicatedServer
{
    public class ClientSession
    {
        public IPEndPoint EndPoint { get; }
        public DateTime LastSeenUtc { get; set; }

        public ClientSession(IPEndPoint endPoint)
        {
            EndPoint = endPoint;
            LastSeenUtc = DateTime.UtcNow;
        }
    }
}