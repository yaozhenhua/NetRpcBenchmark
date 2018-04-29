// <copyright file="NetRpcPerfServer.cs" company="Zhenhua Yao">
// Copyright (c) Zhenhua Yao. All rights reserved.
// </copyright>

namespace NetRpcPerf
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Bond.Grpc;
    using EchoBond;
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

            var serverProto = new Server
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

            serverProto.Start();
            Console.WriteLine($"Protobuf/gRPC Echo service started at port {serverProto.Ports.First().BoundPort}. Press <Enter> to stop...");

            var serverBond = new Server
            {
                Services =
                {
                    EchoBond.Echo.BindService(new EchoBondImpl()),
                },
                Ports =
                {
                    new ServerPort("0.0.0.0", port + 1, ServerCredentials.Insecure),
                },
            };

            serverBond.Start();
            Console.WriteLine($"Bond/gRPC Echo service started at port {serverBond.Ports.First().BoundPort}. Press <Enter> to stop...");

            Console.ReadLine();
            serverProto.ShutdownAsync().GetAwaiter().GetResult();
            serverBond.ShutdownAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Protobuf over gRPC implementation of Echo service
        /// </summary>
        internal class EchoProtoImpl : EchoProto.Echo.EchoBase
        {
            /// <inheritdoc />
            public override Task<EchoProto.Message> Echo(EchoProto.Message request, ServerCallContext context) =>
                Task.FromResult(new EchoProto.Message { Text = request.Text });
        }

        /// <summary>
        /// Bond over gRPC implementation of Echo service
        /// </summary>
        internal class EchoBondImpl : EchoBond.Echo.EchoBase
        {
            ///  <inheritdoc />
            public override Task<IMessage<EchoMessage>> Echo(IMessage<EchoMessage> request, ServerCallContext context)
            {
                var args = request.Payload.Deserialize();
                var reply = new EchoBond.EchoMessage { Text = args.Text, };
                return Task.FromResult(Bond.Grpc.Message.From(reply));
            }
        }
    }
}