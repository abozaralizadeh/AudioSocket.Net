using System;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AudioSocket.Net.Helper;
using NetCoreServer;

namespace AudioSocket.Net
{
    public class AudioSocketServerSTT : TcpServer
    {
        protected BridgeHelper bridgeHelper;
        public AudioSocketServerSTT(string address, int port, BridgeHelper bridgeHelper) : base(address, port) {
            this.bridgeHelper = bridgeHelper;
        }

        protected override TcpSession CreateSession() { return new AudioSocketSessionSTT(this, bridgeHelper); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"AudioSocketSTT TCP server caught an error with code {error}");
        }
    }

    public class AudioSocketSessionSTT  : AudioSocketBaseSession
    {
        private STTHelper sttHelper;

        public AudioSocketSessionSTT(TcpServer server, BridgeHelper bridgeHelper) : base(server) {
            CurrentIndex = 0;
            Remained = 0;
            LastType = null;
            UuidString = string.Empty;
            sttHelper = new STTHelper(this, bridgeHelper);
        }

        public override void OnKindIDReceived(byte[] buffer)
        {
            Console.WriteLine($"Session {this.Id} Socket server received message: KindID 0x01");
        }

        public override void OnKindHangupReceived()
        {
            Console.WriteLine($"Session {this.Id} Socket server received message: KindHangup 0x00");

            base.StopBufferProcessing();
        }

        public override void OnKindErrorReceived(byte[] buffer, string errorCode)
        {
            Console.WriteLine($"Session {this.Id} Socket server received message: KindError 0xff");

            base.StopBufferProcessing();
        }

        public override void OnKindSlinReceived(byte[] buffer, byte[] payloadToStream)
        {
            sttHelper.FromStream(payloadToStream);
        }

        public override void OnFallbackReceived()
        {
            Console.WriteLine($"Session {this.Id} Type Unrecognised");

            base.StopBufferProcessing();
        }
    }
}

