﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

const int BUFFER_SIZE = 1_024;
const byte KindHangup = 0x00;
const byte KindID = 0x01;
const byte KindSlin = 0x10;
const byte KindError = 0xff;

//var lengthbytes = new byte[] { 0x00, 0x10 };  
//var x = ToDecimal(lengthbytes);

while (true)
{
    try
    {
        await StartSocket();
    }
    catch { }
}


async Task StartSocket() {

    // ToDo Delete
    File.Delete("sampleoutputstream.slin");

    //Dns.GetHostName()
    //IPHostEntry ipHostInfo = await Dns.GetHostEntryAsync("localhost");


    IPAddress ipAddress = new IPAddress(new byte[] { 0x7f, 0x00, 0x00, 0x01}); //ipHostInfo.AddressList[0];

    IPEndPoint ipEndPoint = new(ipAddress, 5044);

    using Socket listener = new(
    ipEndPoint.AddressFamily,
    SocketType.Stream,
    ProtocolType.Tcp);

    listener.Bind(ipEndPoint);
    listener.Listen(100);

    string uuidString;
    var handler = await listener.AcceptAsync();
    var currentIndex = 0;
    var remained = 0;
    byte? lastType = null;
    

    //await handler.SendAsync(terminateBytes, 0);

    try
    {
        while (true)
        {
            currentIndex = 0;
            // Receive message.
            var buffer = new byte[BUFFER_SIZE];
            var received = await handler.ReceiveAsync(buffer, SocketFlags.None);
            //var response = Encoding.UTF8.GetString(buffer, 0, received);


            if (buffer.Length > 0)
            {

                while (currentIndex < buffer.Length)
                {
                    if (remained == 0)
                    {
                        lastType = buffer[currentIndex];
                    }

                    if (lastType == KindHangup /* is end of message */)
                    {
                        Console.WriteLine(
                            $"Socket server received message: 0x00");

                        var echoBytes = new byte[] { 0x00, 0x00, 0x00 };
                        //await handler.SendAsync(echoBytes, 0);
                        Console.WriteLine(
                            $"Socket server sent acknowledgment: 0x00,0x00, 0x00");

                        break;
                    }

                    else if (lastType == KindID)
                    {
                        var length = ToDecimal(buffer.Take(new Range(1 + currentIndex, 3 + currentIndex)).ToArray());
                        var UUID = buffer.Take(new Range(3 + currentIndex, (Index)(3 + currentIndex + length)));
                        uuidString = ByteArrayToString(UUID.ToArray());
                        currentIndex += (int)(3 + length);

                        continue;
                    }

                    else if (lastType == KindError)
                    {
                        Console.WriteLine($"Socket error recieved");

                        var length = ToDecimal(buffer.Take(new Range(1 + currentIndex, 3 + currentIndex)).ToArray());
                        var errorCode = buffer.Take(new Range(3 + currentIndex, (Index)(3 + currentIndex + length)));
                        var errorCodeString = ByteArrayToString(errorCode.ToArray());
                        // ToDo handle the error
                        currentIndex +=  (int)(3 + length);
                    }

                    else if (lastType == KindSlin)
                    {
                        Console.WriteLine("audio received");

                        if (remained == 0)
                        {
                            var length = ToDecimal(buffer.Take(new Range(1 + currentIndex, 3 + currentIndex)).ToArray());
                            remained = (int)length;
                            currentIndex += 3;
                        }

                        byte[] payloadToStream;

                        if (remained > BUFFER_SIZE - currentIndex)
                        {
                            payloadToStream = buffer.Take(new Range(currentIndex, BUFFER_SIZE)).ToArray();
                            remained = remained - (BUFFER_SIZE - currentIndex);
                            currentIndex = BUFFER_SIZE;

                        }
                        else
                        {
                            payloadToStream = buffer.Take(new Range(currentIndex, currentIndex + remained)).ToArray();
                            currentIndex = currentIndex + remained;
                            remained = 0;

                        }
                        // ToDo Stream the data to STT
                        // CognitiveService.push(payloadToStream);
                        try
                        {
                            var path = "sampleoutputstream.slin";

                            var fileBytes = File.ReadAllBytes(path);
                            File.WriteAllBytes("sampleoutputstream.slin", fileBytes.Concat(payloadToStream).ToArray());
                        }
                        catch {
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
    } catch (Exception ex) {
        Console.WriteLine(ex);
        listener.Shutdown(SocketShutdown.Both);
    }
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