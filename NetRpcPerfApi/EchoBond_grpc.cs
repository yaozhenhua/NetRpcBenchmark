
//------------------------------------------------------------------------------
// This code was generated by a tool.
//
//   Tool : Bond Compiler 0.10.1.0
//   File : EchoBond_grpc.cs
//
// Changes to this file may cause incorrect behavior and will be lost when
// the code is regenerated.
// <auto-generated />
//------------------------------------------------------------------------------


// suppress "Missing XML comment for publicly visible type or member"
#pragma warning disable 1591


#region ReSharper warnings
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable RedundantNameQualifier
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
// ReSharper disable UnusedParameter.Local
// ReSharper disable RedundantUsingDirective
#endregion


namespace EchoBond
{
    using System.Collections.Generic;

    [System.CodeDom.Compiler.GeneratedCode("gbc", "0.10.1.0")]
    public static class Echo 
    {
        static readonly string ServiceName = "EchoBond.Echo";

        static readonly global::Grpc.Core.Method<global::Bond.Grpc.IMessage<EchoMessage>, global::Bond.Grpc.IMessage<EchoMessage>> Method_Echo = new global::Grpc.Core.Method<global::Bond.Grpc.IMessage<EchoMessage>, global::Bond.Grpc.IMessage<EchoMessage>>(
            global::Grpc.Core.MethodType.Unary,
            ServiceName,
            "Echo",
            global::Bond.Grpc.Marshaller<EchoMessage>.Instance,
            global::Bond.Grpc.Marshaller<EchoMessage>.Instance);

        public abstract class EchoBase
        {
            public abstract global::System.Threading.Tasks.Task<global::Bond.Grpc.IMessage<EchoMessage>> Echo(global::Bond.Grpc.IMessage<EchoMessage> request, global::Grpc.Core.ServerCallContext context);
        }

        public class EchoClient : global::Grpc.Core.ClientBase<EchoClient>
        {
            public EchoClient(global::Grpc.Core.Channel channel) : base(channel)
            {
            }

            protected EchoClient() : base()
            {
            }

            protected EchoClient(global::Grpc.Core.ClientBase.ClientBaseConfiguration configuration) : base(configuration)
            {
            }

            public virtual global::Grpc.Core.AsyncUnaryCall<global::Bond.Grpc.IMessage<EchoMessage>> EchoAsync(EchoMessage request, global::Grpc.Core.Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
            {
                var message = global::Bond.Grpc.Message.From(request);
                return EchoAsync(message, new global::Grpc.Core.CallOptions(headers, deadline, cancellationToken));
            }

            public virtual global::Grpc.Core.AsyncUnaryCall<global::Bond.Grpc.IMessage<EchoMessage>> EchoAsync(global::Bond.Grpc.IMessage<EchoMessage> request, global::Grpc.Core.CallOptions options)
            {
                return CallInvoker.AsyncUnaryCall(Method_Echo, null, options, request);
            }

            protected override EchoClient NewInstance(global::Grpc.Core.ClientBase.ClientBaseConfiguration configuration)
            {
                return new EchoClient(configuration);
            }
        }

        public static global::Grpc.Core.ServerServiceDefinition BindService(EchoBase serviceImpl)
        {
            return global::Grpc.Core.ServerServiceDefinition.CreateBuilder()
                    .AddMethod(Method_Echo, serviceImpl.Echo)
                    .Build();
        }
    }

} // EchoBond