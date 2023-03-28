using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetCoreServer;

namespace AudioSocket.Net
{
    public class AudioSocketServer : TcpServer
    {
        
        public AudioSocketServer(string address, int port) : base(address, port) {}

        protected override TcpSession CreateSession() { return new AudioSocketSession(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"AudioSocket TCP server caught an error with code {error}");
        }
    }

    class AudioSocketSession : TcpSession
    {
        public AudioSocketSession(TcpServer server) : base(server) { }

        protected override void OnConnected()
        {
            Console.WriteLine($"AudioSocket TCP session with Id {Id} connected!");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"AudioSocket TCP session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            Console.WriteLine("Incoming: " + message);

            // Multicast message to all connected sessions
            Server.Multicast(message);

            // If the buffer starts with '!' the disconnect the current session
            if (message == "!")
                Disconnect();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"AudioSocket TCP session caught an error with code {error}");
        }
    }
}

