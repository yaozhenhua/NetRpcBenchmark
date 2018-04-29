// <copyright file="NetRpcPerfClient.cs" company="Zhenhua Yao">
// Copyright (c) Zhenhua Yao. All rights reserved.
// </copyright>

namespace NetRpcPerfClient
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// RPC Perf Client
    /// </summary>
    internal sealed class NetRpcPerfClient
    {
        private enum ExecutionStage
        {
            WarmUp,
            Running,
            Stopping,
        }

        private static void Main(string[] args)
        {
            RunWcf("localhost", 12345, 100, 20, 50).GetAwaiter().GetResult();
            RunProto("localhost", 12356, 100, 20, 50).GetAwaiter().GetResult();
        }

        private static async Task RunWcf(
            string remoteAddress,
            int remotePort,
            int channelCount,
            int warmupSeconds,
            int measureSeconds)
        {
            Console.WriteLine($"Establishing {channelCount} WCF channels");
            var clients = Enumerable.Range(0, channelCount)
                .Select(x => new NetWcfPerf.WcfEchoClient($"net.tcp://{remoteAddress}:{remotePort}"))
                .ToArray();

            // A sanity check
            if (await clients.First().EchoAsync("haha").ConfigureAwait(false) != "haha")
            {
                throw new Exception("Failed to receive echo message");
            }

            await PerfBenchmark(
                clients,
                client => client.EchoAsync("haha"),
                warmupSeconds,
                measureSeconds)
                .ConfigureAwait(false);

            Parallel.ForEach(clients, c => c.Close());
        }

        private static async Task RunProto(
            string remoteAddress,
            int remotePort,
            int channelCount,
            int warmupSeconds,
            int measureSeconds)
        {
            Console.WriteLine($"Establishing {channelCount} channels");
            var channels = Enumerable.Range(0, channelCount)
                .Select(x => new Grpc.Core.Channel(remoteAddress, remotePort, Grpc.Core.ChannelCredentials.Insecure))
                .ToArray();
            var clients = channels.Select(x => new EchoProto.Echo.EchoClient(x)).ToArray();
            var inputMsg = new EchoProto.Message { Text = "haha" };
            await PerfBenchmark(
                clients,
                async (client) =>
                {
                    await client.EchoAsync(inputMsg);
                },
                warmupSeconds,
                measureSeconds)
                .ConfigureAwait(false);

            await Task.WhenAll(channels.Select(c => c.ShutdownAsync()));
        }

        private static async Task PerfBenchmark<T>(
            T[] clients,
            Func<T, Task> doSomething,
            int warmupSeconds,
            int measureSeconds)
        {
            var stage = ExecutionStage.WarmUp;
            var count = 0;
            var taskCount = 0;
            var maxTaskCount = 128;
            var clock = Stopwatch.StartNew();
            var minTicks = long.MaxValue;
            var maxTicks = long.MinValue;
            var totalTicks = default(double);

            _ = Task.Run(() =>
            {
                var clientsCount = clients.Length;

                while (stage != ExecutionStage.Stopping)
                {
                    SpinWait.SpinUntil(() => taskCount < maxTaskCount);

                    var startTicks = stage == ExecutionStage.Running
                        ? clock.ElapsedTicks
                        : 0L;

                    _ = Task.Run(() => doSomething(clients[count % clientsCount]))
                        .ContinueWith(t =>
                        {
                            Interlocked.Decrement(ref taskCount);
                            Interlocked.Increment(ref count);

                            if (stage == ExecutionStage.Running)
                            {
                                var singleCallDuration = clock.ElapsedTicks - startTicks;

                                // ignore the synchronization issue for now
                                minTicks = minTicks < singleCallDuration ? minTicks : singleCallDuration;
                                maxTicks = maxTicks > singleCallDuration ? maxTicks : singleCallDuration;
                                totalTicks += singleCallDuration;
                            }
                        })
                        .ConfigureAwait(false);

                    Interlocked.Increment(ref taskCount);
                }
            });

            for (int i = 0; i < warmupSeconds + measureSeconds; i++)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                Console.WriteLine($"{DateTime.UtcNow} count={count} tasks={taskCount}");

                if (i == warmupSeconds)
                {
                    Interlocked.Exchange(ref count, 0);
                    clock.Restart();
                    stage = ExecutionStage.Running;

                    Console.WriteLine($"Warmup finished. reset counter.");
                }
            }

            stage = ExecutionStage.Stopping;
            clock.Stop();

            var rate = count / clock.Elapsed.TotalSeconds;
            var minDuration = (double)minTicks / TimeSpan.TicksPerMillisecond;
            var maxDuration = (double)maxTicks / TimeSpan.TicksPerMillisecond;
            var avgDuration = totalTicks / TimeSpan.TicksPerMillisecond / count;
            Console.WriteLine($"Duration={clock.Elapsed} Count={count} Rate={rate}. Min={minDuration} Max={maxDuration} Avg={avgDuration}");
        }
    }
}
