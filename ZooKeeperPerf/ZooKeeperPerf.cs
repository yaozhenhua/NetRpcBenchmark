// <copyright file="ZooKeeperPerf.cs" company="Zhenhua Yao">
// Copyright (c) Zhenhua Yao. All rights reserved.
// </copyright>

namespace ZooKeeperPerf
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using org.apache.zookeeper;
    using org.apache.zookeeper.data;

    /// <summary>
    /// Zoo Keeper performance benchmark
    /// </summary>
    internal sealed class ZooKeeperPerf
    {
        private const string RootNodeName = "/MadariUserData";
        private const int VnetPerThreadCount = 300;
        private const int MappingsPerVnet = 100;
        private const int NodeValueSize = 64;

        private static int nodeCount = 0;

        private static int threadFinished = 0;

        private static async Task Main(string[] args)
        {
            var endpoint = args.Length > 0 ? args[0] : "localhost:2181";

            var zk = new ZooKeeper(endpoint, 10000, null);
            var acls = new List<ACL> { new ACL((int)ZooDefs.Perms.ALL, ZooDefs.Ids.ANYONE_ID_UNSAFE), };

            /*
             * Create this structure:
             *
             *  /MadariUserData
             *      /vnets-{guid}
             *          /mappings/v4ca
             *              random key-value pair, key is random IP address, value is 64 bytes
             */
            if (await zk.existsAsync(RootNodeName) == null)
            {
                await zk.createAsync(RootNodeName, new byte[0], acls, CreateMode.PERSISTENT);
            }

            var threadCount = Environment.ProcessorCount;

            Action showStatus = () => Task.Run(async () =>
            {
                while (threadFinished < threadCount)
                {
                    await Task.Delay(1000);
                    Console.WriteLine($"{DateTime.Now.ToString()} - count={nodeCount}");
                }
            });

            var sw = Stopwatch.StartNew();

            var threads = Enumerable.Range(0, threadCount).Select(x => new Thread(CreateVnetThread)).ToArray();
            Parallel.ForEach(threads, t => t.Start(endpoint));

            showStatus();
            SpinWait.SpinUntil(() => threadFinished >= threadCount);

            sw.Stop();
            var rate = nodeCount / sw.Elapsed.TotalSeconds;

            Console.WriteLine($"Creation completed. Total node count = {nodeCount} QPS = {rate}");

            zk = new ZooKeeper(endpoint, 10000, null);
            var children = (await zk.getChildrenAsync(RootNodeName)).Children;
            Console.WriteLine($"Number of VNETs: {children.Count}");
            var childrenPerThread = children.Count / threadCount;
            threads = Enumerable.Range(0, threadCount).Select(x => new Thread(ReadVnetThread)).ToArray();

            threadFinished = 0;
            nodeCount = 0;
            sw.Restart();
            Parallel.ForEach(
                Enumerable.Range(0, threadCount),
                n => threads[n].Start(
                    Tuple.Create(
                        endpoint,
                        children.Skip(n * childrenPerThread)
                            .Take(Math.Min(childrenPerThread, children.Count - (n * childrenPerThread)))
                            .Select(x => string.Join("/", RootNodeName, x)))));

            showStatus();
            SpinWait.SpinUntil(() => threadFinished >= threadCount);

            sw.Stop();
            rate = nodeCount / sw.Elapsed.TotalSeconds;
            Console.WriteLine($"Read completed. Total node count = {nodeCount} QPS = {rate}");
        }

        private static void CreateVnetThread(object endpoints)
        {
            var zk = new ZooKeeper((string)endpoints, 10000, null);
            var acls = new List<ACL> { new ACL((int)ZooDefs.Perms.ALL, ZooDefs.Ids.ANYONE_ID_UNSAFE), };
            var emptyData = new byte[0];
            var rnd = new Random();

            for (int i = 0; i < VnetPerThreadCount; i++)
            {
                CreateInternal().GetAwaiter().GetResult();
            }

            Interlocked.Increment(ref threadFinished);

            async Task CreateInternal()
            {
                var pk = string.Concat($"{RootNodeName}/vnets-", Guid.NewGuid().ToString());
                await zk.createAsync(pk, emptyData, acls, CreateMode.PERSISTENT);

                pk = string.Concat(pk, "/mappings");
                await zk.createAsync(pk, emptyData, acls, CreateMode.PERSISTENT);

                pk = string.Concat(pk, "/v4ca");
                await zk.createAsync(pk, emptyData, acls, CreateMode.PERSISTENT);

                var tasks = Enumerable.Range(0, MappingsPerVnet)
                    .Select(x => zk.createAsync(string.Concat(pk, "/", new IPAddress(rnd.Next()).ToString()), new byte[NodeValueSize], acls, CreateMode.PERSISTENT))
                    .ToArray();
                await Task.WhenAll(tasks);

                Interlocked.Add(ref nodeCount, MappingsPerVnet + 3);
            }
        }

        private static void ReadVnetThread(object o)
        {
            var input = o as Tuple<string, IEnumerable<string>>;
            var endpoints = input.Item1;
            var vnets = input.Item2;

            var zk = new ZooKeeper((string)endpoints, 10000, null);
            var acls = new List<ACL> { new ACL((int)ZooDefs.Perms.ALL, ZooDefs.Ids.ANYONE_ID_UNSAFE), };

            foreach (var vnet in vnets)
            {
                ReadInternal(vnet).GetAwaiter().GetResult();
            }

            Interlocked.Increment(ref threadFinished);

            async Task ReadInternal(string vnet)
            {
                var parent = string.Join("/", vnet, "mappings", "v4ca");
                var children = (await zk.getChildrenAsync(parent)).Children;
                var tasks = children.Select(child => zk.getDataAsync(string.Join("/", parent, child)));
                await Task.WhenAll(tasks);

                Interlocked.Add(ref nodeCount, children.Count + 1);
            }
        }
    }
}
