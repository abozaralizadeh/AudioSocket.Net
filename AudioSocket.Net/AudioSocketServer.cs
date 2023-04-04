using System;
using System.Drawing;
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
        const byte KindHangup = 0x00;
        const byte KindID = 0x01;
        const byte KindSlin = 0x10;
        const byte KindError = 0xff;

        public int CurrentIndex { get; set; }
        public int Remained { get; set; }
        public byte? LastType { get; set; }
        public string UuidString { get; set; }

        public AudioSocketSession(TcpServer server) : base(server) {
            CurrentIndex = 0;
            Remained = 0;
            LastType = null;
            UuidString = string.Empty;
        }

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

            CurrentIndex = 0;

            GetAudioFromAudioSocket(buffer.Take((int)size).ToArray(), (int)size);

            // Multicast message to all connected sessions
            Server.Multicast(message);

            // If the buffer starts with '!' the disconnect the current session
            if (message == "!")
                Disconnect();
        }

        private void GetAudioFromAudioSocket(byte[] buffer, int bufferSize)
        {
            try
            {
               if (buffer.Length > 0)
                    {

                        while (CurrentIndex < bufferSize)
                        {
                            if (Remained == 0)
                            {
                                LastType = buffer[CurrentIndex];
                            }

                            if (LastType == KindHangup /* is end of message */)
                            {
                                Console.WriteLine(
                                    $"Socket server received message: 0x00");

                                var echoBytes = new byte[] { 0x00, 0x00, 0x00 };
                                //await handler.SendAsync(echoBytes, 0);
                                Console.WriteLine(
                                    $"Socket server sent acknowledgment: 0x00,0x00, 0x00");

                                break;
                            }

                            else if (LastType == KindID)
                            {
                                var length = ToDecimal(buffer.Take(new Range(1 + CurrentIndex, 3 + CurrentIndex)).ToArray());
                                var UUID = buffer.Take(new Range(3 + CurrentIndex, (Index)(3 + CurrentIndex + length)));
                                UuidString = ByteArrayToString(UUID.ToArray());
                                CurrentIndex += (int)(3 + length);

                                continue;
                            }

                            else if (LastType == KindError)
                            {
                                Console.WriteLine($"Socket error recieved");

                                var length = ToDecimal(buffer.Take(new Range(1 + CurrentIndex, 3 + CurrentIndex)).ToArray());
                                var errorCode = buffer.Take(new Range(3 + CurrentIndex, (Index)(3 + CurrentIndex + length)));
                                var errorCodeString = ByteArrayToString(errorCode.ToArray());
                                // ToDo handle the error
                                CurrentIndex += (int)(3 + length);
                            }

                            else if (LastType == KindSlin)
                            {
                                Console.WriteLine("audio received");

                                if (Remained == 0)
                                {
                                    var length = ToDecimal(buffer.Take(new Range(1 + CurrentIndex, 3 + CurrentIndex)).ToArray());
                                    Remained = (int)length;
                                    CurrentIndex += 3;
                                }

                                byte[] payloadToStream;

                                if (Remained > bufferSize - CurrentIndex)
                                {
                                    payloadToStream = buffer.Take(new Range(CurrentIndex, bufferSize)).ToArray();
                                    Remained = Remained - (bufferSize - CurrentIndex);
                                    CurrentIndex = bufferSize;

                                }
                                else
                                {
                                    payloadToStream = buffer.Take(new Range(CurrentIndex, CurrentIndex + Remained)).ToArray();
                                    CurrentIndex = CurrentIndex + Remained;
                                    Remained = 0;

                                }
                                // ToDo Stream the data to STT
                                // CognitiveService.push(payloadToStream);
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
            Console.WriteLine($"AudioSocket TCP session caught an error with code {error}");
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

