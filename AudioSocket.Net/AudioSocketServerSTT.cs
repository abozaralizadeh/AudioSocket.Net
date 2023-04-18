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
        public AudioSocketServerSTT(string address, int port) : base(address, port) {}

        protected override TcpSession CreateSession() { return new AudioSocketSessionSTT(this); }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"AudioSocketSTT TCP server caught an error with code {error}");
        }
    }

    class AudioSocketSessionSTT : TcpSession
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

        private STTHelper sttHelper;
        private TTSHelper ttsHelper;

        public AudioSocketSessionSTT(TcpServer server) : base(server) {
            CurrentIndex = 0;
            Remained = 0;
            LastType = null;
            UuidString = string.Empty;
            sttHelper = new STTHelper(this);
            
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"AudioSocketSTT TCP session with Id {Id} connected!");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"AudioSocketSTT TCP session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            //string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            //Console.WriteLine("Incoming: " + message);

            Console.WriteLine($"AudioSocketSTT received request");
            GetAudioFromAudioSocket(buffer.Take(new Range((Index)offset, (Index)(offset+size))).ToArray(), size);

            // Multicast message to all connected sessions
            //Server.Multicast(message);

            // If the buffer starts with '!' the disconnect the current session
            //if (message == "!")
            //    Disconnect();
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

                            // TODO move to another file
                            // TODO get bottext from uuid
                            var ttsHelper = new TTSHelper(this, null);
                            while (true) // Send audio from tts
                            {
                                var size = ttsHelper.ConvertTextToSpeechAsync(sentbuffer);

                                if (size > 0)
                                {
                                    var headerBytes = new byte[] { 0x10 };

                                    headerBytes = headerBytes.Concat(BitConverter.GetBytes(size)).ToArray();
                                    this.Send(headerBytes);
                                    this.Send(sentbuffer.Take((int)size).ToArray());
                                }
                                else
                                {
                                    var hangupBytes = new byte[] { 0x00, 0x00, 0x00 };
                                    this.Send(hangupBytes);
                                }

                            }

                            continue;
                        }

                        else if (LastType == KindError)
                        {
                            Console.WriteLine(
                                $"Socket server received message: KindError 0xff");

                            var length = ToDecimal(buffer.Take(new Range((int)(1 + CurrentIndex), (int)(3 + CurrentIndex))).ToArray());
                            var errorCode = buffer.Take(new Range((int)(3 + CurrentIndex), (Index)(3 + CurrentIndex + length)));
                            var errorCodeString = ByteArrayToString(errorCode.ToArray());
                            // ToDo handle the error
                            CurrentIndex += (int)(3 + length);
                        }

                        else if (LastType == KindSlin)
                        {
                            Console.WriteLine($"Socket server received message: KindSlin 0x10");

                            if (Remained == 0)
                            {
                                var length = ToDecimal(buffer.Take(new Range((int)(1 + CurrentIndex), (int)(3 + CurrentIndex))).ToArray());
                                Remained = (int)length;
                                CurrentIndex += 3;
                            }

                            byte[] payloadToStream;

                            if (Remained > bufferSize - CurrentIndex)
                            {
                                payloadToStream = buffer.Take(new Range((int)CurrentIndex, (int)bufferSize)).ToArray();
                                Remained = Remained - (bufferSize - CurrentIndex);
                                CurrentIndex = bufferSize;
                            }
                            else
                            {
                                payloadToStream = buffer.Take(new Range((int)CurrentIndex, (int)(CurrentIndex + Remained))).ToArray();
                                CurrentIndex = CurrentIndex + Remained;
                                Remained = 0;
                            }
                            // ToDo Stream the data to STT
                            sttHelper.FromStream(payloadToStream, Uuid);
                            
                            try
                            {
                                var path = "sampleoutputstream.slin";

                                var fileBytes = File.ReadAllBytes(path);
                                File.WriteAllBytes("sampleoutputstream.slin", fileBytes.Concat(payloadToStream).ToArray());
                            }
                            catch(Exception ex)
                            {
                                File.WriteAllBytes("sampleoutputstream.slin", payloadToStream);
                            }
                            // event arrived!
                            // remained = 0;
                            // currentIndex = 0;
                            // var terminateBytes = new byte[] { 0x00, 0x00, 0x00 };
                            // var x = terminateBytes.Take(new Range(0, 2));
                            // break;

                            //var echoBytes = new byte[] { 0x00, 0x00, 0x00 };
                            //handler.SendAsync(echoBytes, 0).Ge;
                            //Server.Multicast(echoBytes);
                        }
                        else
                        {
                            Console.WriteLine(
                                $"Type Unrecognised");
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
            Console.WriteLine($"AudioSocketSTT TCP session caught an error with code {error}");
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

