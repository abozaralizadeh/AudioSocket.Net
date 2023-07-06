using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AudioSocket.Net;
using AudioSocket.Net.Helper;
using Microsoft.Extensions.Configuration;
using NetCoreServer;

internal class Program
{
    private static void Main(string[] args)
    {
        var configuration = SettingHelper.GetConfigurations();

        // TCP server port
        int port = configuration.GetValue<int>("AudioSocket:Port");
        string address = configuration.GetValue<string>("AudioSocket:IpAddress") ?? "0.0.0.0";
        string? serverType = configuration.GetValue<string>("AudioSocket:ServerType");

        if (serverType is null)
            Console.Write("Define the server type settings!");

        var cacheHelper = new MemcachedHelper();
        var vvbHelper = new VVBHelper(cacheHelper);

        TcpServer AudioSocketServer;

        if (serverType is "STT")
            AudioSocketServer = new AudioSocketServerSTT(address, port, vvbHelper);
        else if (serverType is "TTS")
            AudioSocketServer = new AudioSocketServerTTS(address, port, vvbHelper);
        else
            throw new Exception("server type is unknown," +
                "please set the 'AudioSocket:ServerType'" +
                "with one of the following options: " +
                "\n- TTS" +
                "\n- STT");

        AudioSocketServer.Start();

        Worker workerObject = new Worker();
        Thread workerThread = new Thread(() => workerObject.DoWork(AudioSocketServer));

        // Start the worker thread.
        workerThread.Start();

        workerThread.Join();
    }

    public class Worker
    {
        // This method is called when the thread is started.
        public void DoWork(TcpServer AudioSocketServer)
        {
            while (true)
            {
                if (AudioSocketServer != null && !AudioSocketServer.IsStarted)
                    AudioSocketServer.Restart();

                Thread.Sleep(1000);
            }
        }

        public void RequestStop()
        {
            _shouldStop = true;
        }

        // Keyword volatile is used as a hint to the compiler that this data
        // member is accessed by multiple threads.
        private volatile bool _shouldStop;
    }
}