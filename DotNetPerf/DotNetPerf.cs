// Reference: https://blogs.msdn.microsoft.com/dotnet/2017/06/07/performance-improvements-in-net-core/

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

class DotNetPerf
{
    private static readonly Action<string> log = Console.WriteLine;

    private static void Main()
    {
        SocketNetworkStreamPerf().GetAwaiter().GetResult();
        TcpPingPongPerf();
        TcpPingPongPerfAsync().GetAwaiter().GetResult();
        TcpServerQpsAsync(100);
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

                    var requestCount = 1_000_000;
                    for (int i = 0; i < requestCount; i++)
                    {
                        await clientStream.WriteAsync(data, 0, data.Length);
                    }

                    client.Shutdown(SocketShutdown.Send);
                    serverCopyAll.Wait();
                    sw.Stop();

                    var qps = requestCount / sw.Elapsed.TotalSeconds;
                    log($"SocketNetworkStreamPerf: Elapsed={sw.Elapsed} QPS={qps}");
                    log($"  Gen0={GC.CollectionCount(0) - gen0} Gen1={GC.CollectionCount(1) - gen1} Gen2={GC.CollectionCount(2) - gen2}");
                }
            }
        }
    }

    static void TcpPingPongPerf()
    {
        var server = new TcpListener(IPAddress.Any, 0);
        server.Start();
        log($"Server started at {server.LocalEndpoint}");

        _ = Task.Run(() =>
        {            
            var bytes = new byte[1024];
            try
            {
                while (true)
                {
                    var acceptedClient = server.AcceptTcpClient();
                    log($"Connected with client!");

                    var stream = acceptedClient.GetStream();
                    int count;
                    while ((count = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        stream.Write(bytes, 0, count);
                    }
                }
            }
            catch (SocketException e)
            {
                log($"SocketException: {e}");
            }
            finally
            {
                server.Stop();
            }
        });

        using (var client = new TcpClient(IPAddress.Loopback.ToString(), ((IPEndPoint)server.LocalEndpoint).Port))
        using (var clientStream = client.GetStream())
        {
            var clientBuffer = new byte[1024];
            var rnd = new Random();

            var sw = new Stopwatch();
            int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
            sw.Start();

            var requestCount = 1_000_000;
            for (int i = 0; i < requestCount; i++)
            {
                rnd.NextBytes(clientBuffer);
                clientStream.Write(clientBuffer, 0, clientBuffer.Length);
                clientStream.Read(clientBuffer, 0, clientBuffer.Length);
            }

            sw.Stop();

            var qps = requestCount / sw.Elapsed.TotalSeconds;
            log($"TcpPingPongPerf: Elapsed={sw.Elapsed} QPS={qps}");
            log($"  Gen0={GC.CollectionCount(0) - gen0} Gen1={GC.CollectionCount(1) - gen1} Gen2={GC.CollectionCount(2) - gen2}");
        }
    }

    static async Task TcpPingPongPerfAsync()
    {
        var server = new TcpListener(IPAddress.Any, 0);
        server.Start();
        log($"Server started at {server.LocalEndpoint}");

        _ = Task.Run(async () =>
        {            
            var bytes = new byte[1024];
            try
            {
                while (true)
                {
                    var acceptedClient = await server.AcceptTcpClientAsync();
                    log($"Connected with client!");

                    var stream = acceptedClient.GetStream();
                    int count;
                    while ((count = await stream.ReadAsync(bytes, 0, bytes.Length)) != 0)
                    {
                        await stream.WriteAsync(bytes, 0, count);
                    }
                }
            }
            catch (SocketException e)
            {
                log($"SocketException: {e}");
            }
            finally
            {
                server.Stop();
            }
        });

        using (var client = new TcpClient(IPAddress.Loopback.ToString(), ((IPEndPoint)server.LocalEndpoint).Port))
        using (var clientStream = client.GetStream())
        {
            var clientBuffer = new byte[1024];
            var rnd = new Random();

            var sw = new Stopwatch();
            int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
            sw.Start();

            var requestCount = 1_000_000;
            for (int i = 0; i < requestCount; i++)
            {
                rnd.NextBytes(clientBuffer);
                await clientStream.WriteAsync(clientBuffer, 0, clientBuffer.Length);
                await clientStream.ReadAsync(clientBuffer, 0, clientBuffer.Length);
            }

            sw.Stop();

            var qps = requestCount / sw.Elapsed.TotalSeconds;
            log($"TcpPingPongPerfAsync: Elapsed={sw.Elapsed} QPS={qps}");
            log($"  Gen0={GC.CollectionCount(0) - gen0} Gen1={GC.CollectionCount(1) - gen1} Gen2={GC.CollectionCount(2) - gen2}");
        }
    }

    static void TcpServerQpsAsync(int connectionCount)
    {
        var server = new TcpListener(IPAddress.Any, 0);
        server.Start();
        log($"Server started at {server.LocalEndpoint}");

        _ = Task.Run(async () =>
        {            
            try
            {
                while (true)
                {
                    var acceptedClient = await server.AcceptTcpClientAsync();

                    _ = Task.Run(async() =>
                    {
                        var stream = acceptedClient.GetStream();

                        var bytes = new byte[1024];
                        int count;
                        while ((count = await stream.ReadAsync(bytes, 0, bytes.Length)) != 0)
                        {
                            await stream.WriteAsync(bytes, 0, count);
                        }
                    });
                }
            }
            catch (SocketException e)
            {
                log($"SocketException: {e}");
            }
            finally
            {
                server.Stop();
            }
        });

        TcpClient[] clients = null;
        NetworkStream[] clientStreams = null;

        try
        {
            clients = Enumerable.Range(0, connectionCount)
                .Select(n => new TcpClient(IPAddress.Loopback.ToString(), ((IPEndPoint)server.LocalEndpoint).Port))
                .ToArray();
            clientStreams = clients.Select(c => c.GetStream()).ToArray();
            var clientBuffers = clients.Select(c => new byte[1024]).ToArray();

            var rnd = new Random();

            var sw = new Stopwatch();
            int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
            sw.Start();

            int inflightTaskCount = 0;

            int requestCount = 1_000_000;
            for (int i = 0; i < requestCount; i++)
            {
                var clientBuffer = clientBuffers[i % connectionCount];
                var clientStream = clientStreams[i % connectionCount];

                rnd.NextBytes(clientBuffer);
                SpinWait.SpinUntil(() => inflightTaskCount < connectionCount);

                _ = clientStream.WriteAsync(clientBuffer, 0, clientBuffer.Length)
                    .ContinueWith(t =>
                    {
                        _ = clientStream.ReadAsync(clientBuffer, 0, clientBuffer.Length)
                            .ContinueWith(t2 =>
                            {
                                Interlocked.Decrement(ref inflightTaskCount);
                            });
                    });

                Interlocked.Increment(ref inflightTaskCount);
            }

            SpinWait.SpinUntil(() => inflightTaskCount == 0);
            sw.Stop();

            var qps = requestCount / sw.Elapsed.TotalSeconds;
            log($"TcpServerQpsAsync: Elapsed={sw.Elapsed} QPS={qps}");
            log($"  Gen0={GC.CollectionCount(0) - gen0} Gen1={GC.CollectionCount(1) - gen1} Gen2={GC.CollectionCount(2) - gen2}");
        }
        finally
        {
            Parallel.ForEach(clientStreams, s => s?.Dispose());
            Parallel.ForEach(clients, c => c?.Dispose());
        }
    }
}
