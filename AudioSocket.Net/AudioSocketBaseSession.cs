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
    public abstract class AudioSocketBaseSession : TcpSession
    {
        //TODO: create enum
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
        public byte[] Sentbuffer { get; set; } = new byte[320];
        public bool processBufferBlocked;

        public AudioSocketBaseSession(TcpServer server) : base(server)
        {
            CurrentIndex = 0;
            Remained = 0;
            LastType = default;
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
            ProcessBuffer(buffer.Take(new Range((Index)offset, (Index)(offset + size))).ToArray(), size);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"AudioSocketTTS TCP session caught an error with code {error}");
        }

        public void StopBufferProcessing()
        {
            processBufferBlocked = true;
        } 

        #region audiosocketmethods

        /// <summary>
        /// Process AudioSocket KindId
        /// </summary>
        /// <param name="buffer"></param>
        public abstract void OnKindIDReceived(byte[] buffer);

        /// <summary>
        /// Process AudioSocket KindHangup
        /// </summary>
        /// <param name="buffer"></param>
        public abstract void OnKindHangupReceived();

        /// <summary>
        /// Process AudioSocket KindError
        /// </summary>
        /// <param name="buffer"></param>
        public abstract void OnKindErrorReceived(byte[] buffer, string errorCode);

        /// <summary>
        /// Process AudioSocket KindSlin
        /// </summary>
        /// <param name="buffer"></param>
        public abstract void OnKindSlinReceived(byte[] buffer, byte[] payloadToStream);

        /// <summary>
        /// Process AudioSocket Fallback
        /// </summary>
        /// <param name="buffer"></param>
        public abstract void OnFallbackReceived();

        #endregion

        #region privatemethods

        private void ProcessBuffer(byte[] buffer, long bufferSize)
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

                        if (LastType == KindHangup)
                        {
                            OnKindHangupReceived();

                            if(processBufferBlocked)
                                break;
                        }

                        else if (LastType == KindID)
                        {
                            var length = ByteArrayToDecimal(buffer.Take(new Range((int)(1 + CurrentIndex), (int)(3 + CurrentIndex))).ToArray());
                            var UUID = buffer.Take(new Range((int)(3 + CurrentIndex), (Index)(3 + CurrentIndex + length)));
                            Uuid = UUID.ToArray();
                            UuidString = ByteArrayToString(UUID.ToArray());
                            CurrentIndex += (int)(3 + length);

                            OnKindIDReceived(buffer);

                            if (processBufferBlocked)
                                break;
                        }

                        else if (LastType == KindError) 
                        {
                            var length = ByteArrayToDecimal(buffer.Take(new Range((int)(1 + CurrentIndex), (int)(3 + CurrentIndex))).ToArray());
                            var errorCode = buffer.Take(new Range((int)(3 + CurrentIndex), (Index)(3 + CurrentIndex + length)));
                            var errorCodeString = ByteArrayToString(errorCode.ToArray());
                            CurrentIndex += (int)(3 + length);

                            OnKindErrorReceived(buffer, errorCodeString);

                            if (processBufferBlocked)
                                break;
                        }

                        else if (LastType == KindSlin)
                        {
                            //TODO: this could not be executed for the STT, maybe implement a OnKindSlinReceiving to block the payload to be calculated
                            if (Remained == 0)
                            {
                                var length = ByteArrayToDecimal(buffer.Take(new Range((int)(1 + CurrentIndex), (int)(3 + CurrentIndex))).ToArray());
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
                            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                            OnKindSlinReceived(buffer, payloadToStream);

                            if (processBufferBlocked)
                                break;
                        }

                        else
                        {
                            OnFallbackReceived();

                            if (processBufferBlocked)
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

        #endregion

        #region staticmethods

        /// <summary>
        /// Converts byte array to decimal
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static decimal ByteArrayToDecimal(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse<Byte>(bytes);
            return BitConverter.ToUInt16(bytes);
        }

        /// <summary>
        /// Converts byte array to string
        /// </summary>
        /// <param name="ba"></param>
        /// <returns></returns>
        public static string ByteArrayToString(byte[] bytes)
        {
            return BitConverter.ToString(bytes);
        }

        #endregion
    }
}

