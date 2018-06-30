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
    private const int PacketLength = 1024;

    private static readonly Action<string> log = Console.WriteLine;

    private static void Main()
    {
        SocketNetworkStreamPerf().GetAwaiter().GetResult();
        SocketAsyncEventArgsPerf();
        TcpPingPongPerf();
        TcpPingPongPerfAsync().GetAwaiter().GetResult();
        TcpServerQpsAsync(100);
        LibUvPingPongAsync("LibUvPingPong", 1).GetAwaiter().GetResult();
        LibUvPingPongAsync("LibUvThroughput", 100).GetAwaiter().GetResult();
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

                    byte[] data = new byte[PacketLength];
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
                    log($"  Gen0={GC.CollectionCount(0) - gen0} Gen1={GC.CollectionCount(1) - gen1} Gen2={GC.CollectionCount(2) - gen2}\n");
                }
            }
        }
    }

    static void SocketAsyncEventArgsPerf()
    {
        // Reference: https://msdn.microsoft.com/en-us/library/system.net.sockets.socketasynceventargs(v=vs.110).aspx
        // reference: https://www.codeproject.com/Articles/22918/How-To-Use-the-SocketAsyncEventArgs-Class
        int packetCount = 0;

        // Start the TCP server
        var port = StartServer();
        log($"Socket TCP server started at port {port}.");

        // Start the client
        var connectedEvent = new AutoResetEvent(false);
        var endpoint = new IPEndPoint(IPAddress.Loopback, port);
        var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        var connectArgs = new SocketAsyncEventArgs();
        connectArgs.UserToken = clientSocket;
        connectArgs.Completed += (sender, e) => connectedEvent.Set();
        connectArgs.RemoteEndPoint = endpoint;

        clientSocket.ConnectAsync(connectArgs);
        connectedEvent.WaitOne();
        log($"Client is connected to the server");

        if (connectArgs.SocketError != SocketError.Success)
        {
            throw new SocketException((int)connectArgs.SocketError);
        }

        var stop = false;
        StartSend(clientSocket, new byte[PacketLength]);

        Task.Run(
            () =>
            {
                while (!stop)
                {
                    Thread.Sleep(5 * 1000);
                    log($"  {DateTime.Now.ToString()} count = {packetCount}");
                }
            });

        log($"Warm up for 30 seconds");
        Thread.Sleep(30 * 1000);
        log($"Start measurement...");

        var sw = new Stopwatch();

        int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);

        Interlocked.Exchange(ref packetCount, 0);
        sw.Start();

        Thread.Sleep(30 * 1000);
        stop = true;
        sw.Stop();
        var rate = packetCount / sw.Elapsed.TotalSeconds;

        clientSocket.Disconnect(false);

        log($"{nameof(SocketAsyncEventArgsPerf)} finished. QPS={rate}");
        log($"  Gen0={GC.CollectionCount(0) - gen0} Gen1={GC.CollectionCount(1) - gen1} Gen2={GC.CollectionCount(2) - gen2}");

        void StartSend(Socket socket, byte[] buffer)
        {
            var sendEventArgs = new SocketAsyncEventArgs();
            sendEventArgs.SetBuffer(buffer, 0, buffer.Length);
            sendEventArgs.UserToken = socket;
            sendEventArgs.RemoteEndPoint = socket.RemoteEndPoint;
            sendEventArgs.Completed += SendRecvCompletion;

            if (!socket.SendAsync(sendEventArgs))
            {
                ProcessSend(sendEventArgs);
            }
        }

        int StartServer(int listeningPort = 0)
        {
            var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, listeningPort));
            serverSocket.Listen(backlog: 1000);

            StartAccept(serverSocket, null);
            return (serverSocket.LocalEndPoint as IPEndPoint).Port;
        }

        void StartAccept(Socket serverSocket, SocketAsyncEventArgs acceptEventArg)
        {
            if (acceptEventArg == null)
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += (sender, eventArgs) => ProcessAccept(serverSocket, eventArgs);
            }
            else
            {
                acceptEventArg.AcceptSocket = null;
            }

            if (!serverSocket.AcceptAsync(acceptEventArg))
            {
                ProcessAccept(serverSocket, acceptEventArg);
            }
        }

        void SendRecvCompletion(object sender, SocketAsyncEventArgs eventArgs)
        {
            switch (eventArgs.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceive(eventArgs);
                    break;

                case SocketAsyncOperation.Send:
                    ProcessSend(eventArgs);
                    break;

                default:
                    throw new Exception($"Unknown last operation: {eventArgs.LastOperation}");
            }
        }

        void ProcessAccept(Socket serverSocket, SocketAsyncEventArgs e)
        {
            var readEventArgs = new SocketAsyncEventArgs();
            readEventArgs.Completed += SendRecvCompletion;
            readEventArgs.UserToken = e.AcceptSocket;
            readEventArgs.SetBuffer(new byte[PacketLength], 0, PacketLength);

            if (!e.AcceptSocket.ReceiveAsync(readEventArgs))
            {
                ProcessReceive(readEventArgs);
            }

            StartAccept(serverSocket, e);
        }

        void ProcessReceive(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref packetCount);

            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                e.SetBuffer(e.Offset, e.BytesTransferred);
                if (!((Socket)e.UserToken).SendAsync(e))
                {
                    ProcessSend(e);
                }
            }
            else
            {
                CloseClientSocket(e);
            }
        }

        void ProcessSend(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                if (!((Socket)e.UserToken).ReceiveAsync(e))
                {
                    ProcessReceive(e);
                }
            }
            else
            {
                CloseClientSocket(e);
            }
        }

        void CloseClientSocket(SocketAsyncEventArgs e)
        {
            log($"Closing socket remote={e.RemoteEndPoint}");
            try
            {
                ((Socket)e.UserToken).Shutdown(SocketShutdown.Send); // TODO: try both
            }
            catch (Exception)
            {
            }

            ((Socket)e.UserToken).Close();
        }
    }

    static void TcpPingPongPerf()
    {
        var server = new TcpListener(IPAddress.Any, 0);
        server.Start();
        log($"Server started at {server.LocalEndpoint}");

        _ = Task.Run(() =>
        {            
            var bytes = new byte[PacketLength];
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
            var clientBuffer = new byte[PacketLength];
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
            var bytes = new byte[PacketLength];
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
            var clientBuffer = new byte[PacketLength];
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

                        var bytes = new byte[PacketLength];
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
            var clientBuffers = clients.Select(c => new byte[PacketLength]).ToArray();

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

    static async Task LibUvPingPongAsync(string testCaseName, int clientCount)
    {
        var port = 12345;
        using (var uvServer = new UvServer(port))
        {
            var serverTask = Task.Run(() => uvServer.Run());
            var sw = new Stopwatch();

            var clients = Enumerable.Range(0, clientCount).Select(_ => new UvClient(port)).ToArray();
            var clientTasks = clients.Select(c => Task.Run(() => c.Run())).ToArray();

            SpinWait.SpinUntil(() => uvServer.ConnectCount >= clientCount);

            log($"All clients are connected, warming up for 30 seconds and runnning for 30 seconds.");
            Thread.Sleep(30_000);

            int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
            uvServer.ReceiveCount = 0;
            sw.Start();

            Thread.Sleep(30_000);

            sw.Stop();
            var totalCount = uvServer.ReceiveCount;

            uvServer.Close();
            Parallel.ForEach(clients, client => client.Close());

            await serverTask;
            await Task.WhenAll(clientTasks);

            var rate = totalCount / sw.Elapsed.TotalSeconds;
            log($"{testCaseName} ClientCount={clientCount} Server receive count = {totalCount} in {sw.Elapsed} QPS={rate}");
            log($"  Gen0={GC.CollectionCount(0) - gen0} Gen1={GC.CollectionCount(1) - gen1} Gen2={GC.CollectionCount(2) - gen2}");
        }
    }
}
