﻿using System;
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
        public AudioSocketServerSTT(string address, int port) : base(address, port) {}

        protected override TcpSession CreateSession() { return new AudioSocketSessionSTT(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"AudioSocketSTT TCP server caught an error with code {error}");
        }
    }

    class AudioSocketSessionSTT  : AudioSocketBaseSession
    {
        private STTHelper sttHelper;

        public AudioSocketSessionSTT(TcpServer server) : base(server) {
            CurrentIndex = 0;
            Remained = 0;
            LastType = null;
            UuidString = string.Empty;
            sttHelper = new STTHelper(this);
        }

        public override void OnKindID(byte[] buffer)
        {
            Console.WriteLine($"Socket server received message: KindID 0x01");
        }

        public override void OnKindHangup()
        {
            Console.WriteLine($"Socket server received message: KindHangup 0x00");

            base.StopBufferProcessing();
        }

        public override void OnKindError(byte[] buffer, string errorCode)
        {
            Console.WriteLine($"Socket server received message: KindError 0xff");

            base.StopBufferProcessing();
        }

        public override void OnKindSlin(byte[] buffer, byte[] payloadToStream)
        {
            sttHelper.FromStream(payloadToStream, Uuid);
        }

        public override void OnFallback()
        {
            Console.WriteLine($"Type Unrecognised");

            base.StopBufferProcessing();
        }
    }
}

