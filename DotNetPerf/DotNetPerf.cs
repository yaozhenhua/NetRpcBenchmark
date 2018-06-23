// Reference: https://blogs.msdn.microsoft.com/dotnet/2017/06/07/performance-improvements-in-net-core/

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

class DotNetPerf
{
    private static void Main()
    {
        SocketNetworkStreamPerf().GetAwaiter().GetResult();
    }

    static async Task SocketNetworkStreamPerf()
    {
        using (Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        using (Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        {
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);

            Task connectTask = Task.Run(() => client.Connect(listener.LocalEndPoint));
            using (Socket server = listener.Accept())
            {
                await connectTask;

                using (var serverStream = new NetworkStream(server))
                using (var clientStream = new NetworkStream(client))
                {
                    Task serverCopyAll = serverStream.CopyToAsync(Stream.Null);

                    byte[] data = new byte[1024];
                    new Random().NextBytes(data);


                    var sw = new Stopwatch();
                    int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
                    sw.Start();

                    for (int i = 0; i < 1_000_000; i++)
                    {
                        await clientStream.WriteAsync(data, 0, data.Length);
                    }

                    client.Shutdown(SocketShutdown.Send);
                    serverCopyAll.Wait();
                    sw.Stop();

                    Console.WriteLine($"SocketNetworkStreamPerf: Elapsed={sw.Elapsed}");
                    Console.WriteLine($"  Gen0={GC.CollectionCount(0) - gen0} Gen1={GC.CollectionCount(1) - gen1} Gen2={GC.CollectionCount(2) - gen2}");
                }
            }
        }
    }
}
