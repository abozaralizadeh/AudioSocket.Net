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

        ////if (serverType is "STT")
        //var AudioSocketServer = new AudioSocketServerSTT(address, port);
        ////else
        //var AudioSocketServerTTS = new AudioSocketServerTTS(address, port);
        var cacheHelper = new MemcachedHelper();

        var audioSocketServerSTT = new AudioSocketServerSTT(address, port);
        var audioSocketServerTTS = new AudioSocketServerTTS(address, 5055, cacheHelper);

        audioSocketServerSTT.Start();
        audioSocketServerTTS.Start();

        Worker workerObject = new Worker();
        Thread workerThread = new Thread(() => workerObject.DoWork(audioSocketServerSTT, audioSocketServerTTS));

        // Start the worker thread.
        workerThread.Start();

        workerThread.Join();
    }

    public class Worker
    {
        // This method is called when the thread is started.
        public void DoWork(AudioSocketServerSTT audioSocketServerSTT, AudioSocketServerTTS audioSocketServerTTS)
        {
            while (true)
            {
                if (audioSocketServerSTT != null && !audioSocketServerSTT.IsStarted)
                    audioSocketServerSTT.Restart();

                if (audioSocketServerTTS != null && !audioSocketServerTTS.IsStarted)
                    audioSocketServerTTS.Restart();

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