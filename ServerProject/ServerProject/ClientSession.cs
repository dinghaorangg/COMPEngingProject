using System;
using System.Net;

namespace DedicatedServer
{
    public class ClientSession
    {
        public IPEndPoint EndPoint { get; }
        public DateTime LastSeenUtc { get; set; }

        public bool HasAcceptedPosition { get; private set; }
        public float LastAcceptedX { get; private set; }
        public float LastAcceptedY { get; private set; }
        public float LastAcceptedZ { get; private set; }
        public DateTime LastAcceptedPositionUtc { get; private set; }

        public ClientSession(IPEndPoint endPoint)
        {
            EndPoint = endPoint;
            LastSeenUtc = DateTime.UtcNow;
        }

        public void SetAcceptedPosition(float x, float y, float z, DateTime utcTime)
        {
            LastAcceptedX = x;
            LastAcceptedY = y;
            LastAcceptedZ = z;
            LastAcceptedPositionUtc = utcTime;
            HasAcceptedPosition = true;
        }
    }
}
