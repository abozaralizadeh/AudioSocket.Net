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

        private MemcachedHelper cacheHelper;

        public AudioSocketServerTTS(string address, int port, MemcachedHelper cacheHelper) : base(address, port) {
            this.cacheHelper = cacheHelper;
        }

        protected override TcpSession CreateSession() { return new AudioSocketSessionTTS(this, cacheHelper); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"AudioSocketTTS TCP server caught an error with code {error}");
        }
    }

    public class AudioSocketSessionTTS : AudioSocketBaseSession
    {
        private MemcachedHelper cacheHelper;

        public AudioSocketSessionTTS(TcpServer server, MemcachedHelper cacheHelper) : base(server)
        {
            this.cacheHelper = cacheHelper;
        }

        public override void OnFallback()
        {
            // Error
            var hangupBytes = new byte[] { 0x00, 0x00, 0x00 };
            this.Send(hangupBytes);
        }

        public override void OnKindError(byte[] buffer, string errorCode)
        {
            Console.WriteLine($"Socket server received message: KindError 0xff");

            //var hangupBytes = new byte[] { 0x00, 0x00, 0x00 };
            //this.Send(hangupBytes);

            base.StopBufferProcessing();
        }

        public override void OnKindHangup()
        {
            Console.WriteLine($"Socket server received message: KindHangup 0x00");

            base.StopBufferProcessing();
        }

        public override void OnKindID(byte[] buffer)
        {
            Console.WriteLine($"Socket server received message: KindID 0x01");

            Console.WriteLine($"UUID: {UuidString}");

            var ttsHelper = new TTSHelper(this, null);

            uint size = 320;
            while (size > 0) // Send audio from tts
            {
                var x = new Stopwatch();
                x.Start();

                size = ttsHelper.ConvertTextToSpeechAsync(Sentbuffer);
                Console.WriteLine($"Size={size}");

                if (size <= 0)
                    break;

                var headerBytes = new byte[] { 0x10 };

                headerBytes = headerBytes.Concat(BitConverter.GetBytes(size).Take(2).Reverse()).ToArray();
                this.Send(headerBytes.Concat(Sentbuffer.Take((int)size)).ToArray());

                Console.WriteLine($"audio buffer sent, size={size}, header: {ByteArrayToString(headerBytes)}, {Sentbuffer.Length}");

                x.Stop();
                Console.WriteLine($"ElapsedMilliseconds: {x.ElapsedMilliseconds}");

                if (x.ElapsedMilliseconds < 20)
                    Task.Delay(20 - (int)x.ElapsedMilliseconds).GetAwaiter().GetResult();
            }

            var hangupBytes = new byte[] { 0x00, 0x00, 0x00 };
            this.Send(hangupBytes);
            Console.WriteLine($"Audio sent finished! basta!");

            base.StopBufferProcessing();
        }

        public override void OnKindSlin(byte[] buffer, byte[] payloadToStream)
        {
            //// Error
            //var hangupBytes = new byte[] { 0x00, 0x00, 0x00 };
            //this.Send(hangupBytes);

            base.StopBufferProcessing();
        }
    }
}

