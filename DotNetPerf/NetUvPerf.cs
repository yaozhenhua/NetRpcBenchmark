using System;
using System.Net;
using System.Threading;
using NetUV.Core.Buffers;
using NetUV.Core.Handles;

sealed class UvServer : IDisposable
{
    private static readonly Action<string> log = Console.WriteLine;

    private readonly Loop loop = new Loop();
    private readonly IPEndPoint endpoint;
    private Tcp server;

    private int receiveCount = 0;
    private int connectCount = 0;

    public UvServer(int port)
    {
        this.endpoint = new IPEndPoint(IPAddress.Any, port);
    }

    public int ReceiveCount
    {
        get { return this.receiveCount; }
        set { Interlocked.Exchange(ref this.receiveCount, value); }
    }

    public int ConnectCount => this.connectCount;

    public void Close() => this.server.CloseHandle();

    public void Dispose() => this.server.Dispose();

    public void Run()
    {
        this.server = this.loop
            .CreateTcp()
            .SimultaneousAccepts(true)
            .Listen(
                this.endpoint,
                (client, error) =>
                {
                    if (error != null)
                    {
                        log($"client connection failed: {error.Message}");
                        client.CloseHandle(handle => handle.Dispose());
                    }
                    else
                    {
                        Interlocked.Increment(ref this.connectCount);
                        client.OnRead(
                            this.OnAccept,
                            (handle, exception) => { log($"read error {error.Message}"); });
                    }
                });

        log($"server started on {this.endpoint}");
        this.loop.RunDefault();
        log($"server loop completed");
    }

    private void OnAccept(Tcp client, ReadableBuffer data)
    {
        if (data.Count == 0)
        {
            log($"server OnAccept: data count is 0");
            return;
        }

        ////log($"server read {data.Count}");
        Interlocked.Increment(ref this.receiveCount);

        // Echo back
        var buffer = new byte[data.Count];
        data.ReadBytes(buffer, buffer.Length);
        data.Dispose();

        var writableBuffer = WritableBuffer.From(buffer);
        client.QueueWriteStream(
            writableBuffer,
            (streamHandle, exception) =>
            {
                writableBuffer.Dispose();
                if (exception != null)
                {
                    log($"server write error: {exception.Message}");
                    streamHandle.CloseHandle(h => h.Dispose());
                }
                else
                {
                    client.OnRead(
                        this.OnAccept,
                        (_h, _e) => { log($"read error {_e.Message}"); });
                }
            });

        ////log($"server wrote {buffer.Length}");
    }
}

sealed class UvClient : IDisposable
{
    private static readonly Action<string> log = Console.WriteLine;

    private readonly int port = 12345;
    private Tcp client;

    public UvClient(int port)
    {
        this.port = port;
    }

    public void Close() => this.client.CloseHandle();

    public void Dispose() => this.client.Dispose();

    public void Run()
    {
        var loop = new Loop();
        this.client = loop
            .CreateTcp()
            .NoDelay(true)
            .ConnectTo(
                new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort),
                new IPEndPoint(IPAddress.Loopback, this.port),
                this.OnConnected);

        log($"client loop starting");
        loop.RunDefault();
        log($"client loop completed");
    }

    private void OnConnected(Tcp client, Exception exception)
    {
        if (exception != null)
        {
            log($"client error: {exception.Message}");
            client.CloseHandle(this.OnClosed);
        }
        else
        {
            var writableBuffer = WritableBuffer.From(new byte[1024]);
            client.QueueWriteStream(
                writableBuffer,
                (streamHandle, error) =>
                {
                    writableBuffer.Dispose();
                    if (error != null)
                    {
                        log($"write error {error}");
                        streamHandle.CloseHandle(this.OnClosed);
                    }
                    else
                    {
                        client.OnRead(
                            this.OnAccept,
                            (_h, _e) => { log($"read error {_e.Message}"); });
                    }
                });
            ////log("client wrote 1024");
        }
    }

    private void OnAccept(StreamHandle stream, ReadableBuffer data)
    {
        if (data.Count == 0)
        {
            log($"client OnAccept: data count is 0");
            return;
        }

        ////log($"client accept {data.Count}");

        // Echo back
        var buffer = new byte[data.Count];
        data.ReadBytes(buffer, buffer.Length); 
        data.Dispose();

        var writableBuffer = WritableBuffer.From(buffer);
        stream.QueueWriteStream(
            writableBuffer,
            (handle, exception) =>
            {
                writableBuffer.Dispose();
                if (exception != null)
                {
                    log($"client write error: {exception.Message}");
                    handle.CloseHandle(h => h.Dispose());
                }
                else
                {
                    stream.OnRead(
                        this.OnAccept,
                        (_h, _e) => { log($"read error {_e.Message}"); });
                }
            });

        ////log($"client wrote {buffer.Length}");
    }

    private void OnClosed(ScheduleHandle handle) => handle.Dispose();
}