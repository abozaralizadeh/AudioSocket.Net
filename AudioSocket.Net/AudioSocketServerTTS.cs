using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AudioSocket.Net.Helper;
using NetCoreServer;

namespace AudioSocket.Net
{
    public class AudioSocketServerTTS : TcpServer
    {
        private VVBHelper vvbHelper;

        public AudioSocketServerTTS(string address, int port, VVBHelper vvbHelper) : base(address, port) {
            this.vvbHelper = vvbHelper;
        }

        protected override TcpSession CreateSession() { return new AudioSocketSessionTTS(this, vvbHelper); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"AudioSocketTTS TCP server caught an error with code {error}");
        }
    }

    public class AudioSocketSessionTTS : AudioSocketBaseSession
    {
        private VVBHelper vvbHelper;
        private TTSHelper? ttsHelper;

        public AudioSocketSessionTTS(TcpServer server, VVBHelper vvbHelper) : base(server)
        {
            this.vvbHelper = vvbHelper;
        }

        public override void OnFallbackReceived()
        {
            // Error
            this.SendHangupMessage();
        }

        public override void OnKindErrorReceived(byte[] buffer, string errorCode)
        {
            Console.WriteLine($"Socket server received message: KindError 0xff");
            base.StopBufferProcessing();
        }

        public override void OnKindHangupReceived()
        {
            Console.WriteLine($"Socket server received message: KindHangup 0x00");

            base.StopBufferProcessing();
        }

        public override void OnKindIDReceived(byte[] buffer)
        {
            Console.WriteLine($"Socket server received message: KindID 0x01");

            Console.WriteLine($"UUID: {UuidString}");

            ttsHelper = new TTSHelper(this, vvbHelper);

            uint size = 320;
            while (size > 0) // Send audio from tts
            {
                var delta = new Stopwatch();
                delta.Start();

                size = ttsHelper.ConvertTextToSpeechAsync(Sentbuffer);
                Console.WriteLine($"Size={size}");

                if (size <= 0)
                    break;

                var headerBytes = new byte[] { 0x10 };

                headerBytes = headerBytes.Concat(BitConverter.GetBytes(size).Take(2).Reverse()).ToArray();
                this.Send(headerBytes.Concat(Sentbuffer.Take((int)size)).ToArray());

                Console.WriteLine($"audio buffer sent, size={size}, header: {ByteArrayToString(headerBytes)}, {Sentbuffer.Length}");

                delta.Stop();
                Console.WriteLine($"ElapsedMilliseconds: {delta.ElapsedMilliseconds}");

                if (delta.ElapsedMilliseconds < 20)
                    Task.Delay(20 - (int) delta.ElapsedMilliseconds).GetAwaiter().GetResult();
            }

            this.SendHangupMessage();
            Console.WriteLine($"TTS Audio sent finished!");

            base.StopBufferProcessing();
        }

        public override void OnKindSlinReceived(byte[] buffer, byte[] payloadToStream)
        {
            base.StopBufferProcessing();
        }
    }
}

