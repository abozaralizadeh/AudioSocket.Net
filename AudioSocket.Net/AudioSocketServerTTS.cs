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
        public AudioSocketServerTTS(string address, int port) : base(address, port) {}

        protected override TcpSession CreateSession() { return new AudioSocketSessionTTS(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"AudioSocketTTS TCP server caught an error with code {error}");
        }
    }

    class AudioSocketSessionTTS : TcpSession
    {
        const byte KindHangup = 0x00;
        const byte KindID = 0x01;
        const byte KindSlin = 0x10;
        const byte KindError = 0xff;

        public long CurrentIndex { get; set; }
        public long Remained { get; set; }
        public byte? LastType { get; set; }
        public byte[]? LastBytes { get; set; } = null;
        public byte[]? Uuid { get; set; } = null;
        public string UuidString { get; set; }

        private byte[] sentbuffer { get; set; } = new byte[320];

        private TTSHelper ttsHelper;

        public AudioSocketSessionTTS(TcpServer server) : base(server) {
            CurrentIndex = 0;
            Remained = 0;
            LastType = null;
            UuidString = string.Empty;
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"AudioSocketTTS TCP session with Id {Id} connected!");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"AudioSocketTTS TCP session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine($"AudioSocketTTS received request");
            GetAudioFromAudioSocket(buffer.Take(new Range((Index)offset, (Index)(offset+size))).ToArray(), size);
        }

        private void GetAudioFromAudioSocket(byte[] buffer, long bufferSize)
        {
            try
            {
                CurrentIndex = 0;

                if (LastBytes is not null)
                {
                    buffer = LastBytes.Concat(buffer).ToArray();
                    LastBytes = null;
                    Console.WriteLine($"broken header attached");
                }

                if (buffer.Length > 0)
                {

                    while (CurrentIndex < bufferSize)
                    {
                        if (Remained == 0)
                        {
                            LastType = buffer[CurrentIndex];
                            if (bufferSize - CurrentIndex < 3)
                            {
                                LastBytes = buffer.TakeLast((int)(bufferSize - CurrentIndex)).ToArray();
                                Console.WriteLine($"broken header arrived");
                                break;
                            }
                        }

                        if (LastType == KindHangup /* is end of message */)
                        {
                            Console.WriteLine(
                                $"Socket server received message: KindHangup 0x00");

                            var echoBytes = new byte[] { 0x00, 0x00, 0x00 };
                            //await handler.SendAsync(echoBytes, 0);
                            Console.WriteLine(
                                $"Socket server sent acknowledgment: 0x00,0x00, 0x00");

                            break;
                        }

                        else if (LastType == KindID)
                        {
                            Console.WriteLine(
                                $"Socket server received message: KindID 0x01");

                            var length = ToDecimal(buffer.Take(new Range((int)(1 + CurrentIndex), (int)(3 + CurrentIndex))).ToArray());
                            var UUID = buffer.Take(new Range((int)(3 + CurrentIndex), (Index)(3 + CurrentIndex + length)));
                            Uuid = UUID.ToArray();
                            UuidString = ByteArrayToString(UUID.ToArray());
                            CurrentIndex += (int)(3 + length);

                            Console.WriteLine(
                                $"UUID: {UuidString}");

                            // TODO get bottext from uuid
                            var ttsHelper = new TTSHelper(this, null);
                            uint size = 320;
                            while (size > 0) // Send audio from tts
                            {

                                
                                var x = new Stopwatch();
                                x.Start();

                                

                                size = ttsHelper.ConvertTextToSpeechAsync(sentbuffer);
                                Console.WriteLine($"Size={size}");

                                if (size <= 0)
                                    break;

                                // TODO to delete
                                var path = "ttsres.slin";
                                try
                                {
                                    var fileBytes = File.ReadAllBytes(path);
                                    File.WriteAllBytes(path, fileBytes.Concat(sentbuffer).ToArray());
                                }
                                catch (Exception ex)
                                {
                                    File.WriteAllBytes(path, sentbuffer);
                                }


                                var headerBytes = new byte[] { 0x10 };

                                headerBytes = headerBytes.Concat(BitConverter.GetBytes(size).Take(2).Reverse()).ToArray();
                                this.Send(headerBytes.Concat(sentbuffer.Take((int)size)).ToArray());
                                //this.Send(sentbuffer.Take((int)size).ToArray());



                                Console.WriteLine($"audio buffer sent, size={size}, header: {ByteArrayToString(headerBytes)}, {sentbuffer.Length}");

                                x.Stop();
                                Console.WriteLine($"ElapsedMilliseconds: {x.ElapsedMilliseconds}");

                                if (x.ElapsedMilliseconds < 20)
                                   Task.Delay(20 - (int)x.ElapsedMilliseconds).GetAwaiter().GetResult();
                            }

                            var hangupBytes = new byte[] { 0x00, 0x00, 0x00 };
                            this.Send(hangupBytes);
                            Console.WriteLine($"Audio sent finished! basta!");
                            break;
                        }

                        else if (LastType == KindError)
                        {
                            break;
                            Console.WriteLine(
                                $"Socket server received message: KindError 0xff");

                            var length = ToDecimal(buffer.Take(new Range((int)(1 + CurrentIndex), (int)(3 + CurrentIndex))).ToArray());
                            var errorCode = buffer.Take(new Range((int)(3 + CurrentIndex), (Index)(3 + CurrentIndex + length)));
                            var errorCodeString = ByteArrayToString(errorCode.ToArray());
                            // ToDo handle the error
                            CurrentIndex += (int)(3 + length);
                            var hangupBytes = new byte[] { 0x00, 0x00, 0x00 };
                            this.Send(hangupBytes);
                        }

                        else if (LastType == KindSlin)
                        {
                            break;
                            // Error
                            var hangupBytes = new byte[] { 0x00, 0x00, 0x00 };
                            this.Send(hangupBytes);
                            break;
                        }

                        else
                        {
                            break;
                            // Error
                            var hangupBytes = new byte[] { 0x00, 0x00, 0x00 };
                            this.Send(hangupBytes);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"AudioSocketTTS TCP session caught an error with code {error}");
        }

        static decimal ToDecimal(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse<Byte>(bytes);
            return BitConverter.ToUInt16(bytes);
        }

        static string ByteArrayToString(byte[] ba)
        {
            return BitConverter.ToString(ba);
        }
    }
}

