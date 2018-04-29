// <copyright file="NetRpcPerfServer.cs" company="Zhenhua Yao">
// Copyright (c) Zhenhua Yao. All rights reserved.
// </copyright>

namespace NetRpcPerf
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using EchoProto;
    using Grpc.Core;

    /// <summary>
    /// RPC perf server
    /// </summary>
    internal sealed class NetRpcPerfServer
    {
        private static void Main(string[] args)
        {
            var port = args.Length > 0
                ? int.Parse(args[0])
                : 12356;

            var server = new Server
            {
                Services =
                {
                    EchoProto.Echo.BindService(new EchoProtoImpl()),
                },
                Ports =
                {
                    new ServerPort("0.0.0.0", port, ServerCredentials.Insecure),
                },
            };

            server.Start();
            Console.WriteLine($"Protobuf/gRPC Echo service started at port {server.Ports.First().BoundPort}. Press <Enter> to stop...");

            Console.ReadLine();
            server.ShutdownAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Protobuf over gRPC implementation of Echo service
        /// </summary>
        internal class EchoProtoImpl : EchoProto.Echo.EchoBase
        {
            /// <inheritdoc />
            public override Task<Message> Echo(Message request, ServerCallContext context) =>
                Task.FromResult(new EchoProto.Message { Text = request.Text });
        }
    }
}