using System;

namespace DedicatedServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            int port = 7777;
            UdpGameServer server = new UdpGameServer(port);
            server.Start();

            Console.WriteLine("Press ENTER to stop server...");
            Console.ReadLine();

            server.Stop();
        }
    }
}