using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AudioSocket.Net;
using AudioSocket.Net.Helper;
using Enyim.Caching.Memcached;
using Microsoft.Extensions.Configuration;
using NetCoreServer;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // connect to a list of memcached servers https://github.com/enyim/Memcached2
        var cluster = new MemcachedCluster("localhost:11211");
        // this is mandatory
        cluster.Start();

        var client = cluster.GetClient();
        await client.SetAsync("hello", "world" );
        await client.AppendAsync("hello", new byte[] { 1, 2, 3, 4 });
        var x = await client.GetAsync("hello");


        // more work

        // stop the cluster
        cluster.Dispose();
    }

    private static void Main2(string[] args)
    {
        TcpServer AudioSocketServer;

        var configuration = SettingHelper.GetConfigurations();

        // TCP server port
        int port = configuration.GetValue<int>("AudioSocket:Port");
        string address = configuration.GetValue<string>("AudioSocket:IpAddress") ?? "0.0.0.0";
        string? serverType = configuration.GetValue<string>("AudioSocket:ServerType");

        if (serverType is null)
        {
            Console.Write("Define the server type settings!");
        }

        Console.WriteLine($"TCP server port: {port}");

        Console.WriteLine();

        // Create a new TCP chat server
        //if (serverType is "STT")
            var AudioSocketServer1 = new AudioSocketServerSTT(address, port);
        //else
            AudioSocketServer = new AudioSocketServerTTS(address, 5055);

        // Start the server
        Console.Write("Server starting...");
        AudioSocketServer1.Start();
        AudioSocketServer.Start();
        Console.WriteLine("Done!");

        //Perform text input
        for (;;)
        {
            string? line = Console.ReadLine();

            // Restart the server
            if (line == "!")
            {
                Console.Write("Server restarting...");
                AudioSocketServer.Restart();

                Console.WriteLine("Done!");
                continue;
            }

            if (line == "Q")
            {
                break;
            }

            // Multicast admin message to all sessions
            line = "Server is shutting down!";
            AudioSocketServer.Multicast(line);
        }

        // Stop the server
        Console.Write("Server stopping...");
        AudioSocketServer.Stop();
        Console.WriteLine("Done!");
    }
}