// <copyright file="NetWcfEchoClient.cs" company="Zhenhua Yao">
// Copyright (c) Zhenhua Yao. All rights reserved.
// </copyright>

namespace NetWcfPerf
{
    using System;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.ServiceModel.Description;
    using System.Threading.Tasks;

    /// <summary>
    /// DotNET core client of Echo service
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    public partial class WcfEchoClient : ClientBase<NetRpcPerf.IWcfEcho>, NetRpcPerf.IWcfEcho
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WcfEchoClient"/> class.
        /// </summary>
        /// <param name="remoteAddress">Remote address of the service</param>
        public WcfEchoClient(string remoteAddress)
            : this(new EndpointAddress(remoteAddress))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WcfEchoClient"/> class.
        /// </summary>
        /// <param name="remoteAddress">Remote address of the service</param>
        public WcfEchoClient(EndpointAddress remoteAddress)
            : base(GetNetTcpBinding(), remoteAddress)
        {
            this.Endpoint.Name = $"{nameof(WcfEchoClient)}.nettcp";
            ConfigureEndpoint(this.Endpoint, this.ClientCredentials);
        }

        /// <summary>
        /// Opens this WCF client
        /// </summary>
        /// <returns>async task object</returns>
        public virtual Task OpenAsync()
        {
            return Task.Factory.FromAsync(
                ((ICommunicationObject)this).BeginOpen(null, null),
                new Action<IAsyncResult>(((ICommunicationObject)this).EndOpen));
        }

        /// <summary>
        /// Closes this WCF client
        /// </summary>
        /// <returns>async task object</returns>
        public virtual Task CloseAsync()
        {
            return Task.Factory.FromAsync(
                ((ICommunicationObject)this).BeginClose(null, null),
                new Action<IAsyncResult>(((ICommunicationObject)this).EndClose));
        }

        /// <inheritdoc />
        public Task<string> EchoAsync(string msg)
        {
            return this.Channel.EchoAsync(msg);
        }

        private static Binding GetNetTcpBinding()
        {
            return new NetTcpBinding
            {
                MaxBufferSize = int.MaxValue,
                ReaderQuotas = System.Xml.XmlDictionaryReaderQuotas.Max,
                MaxReceivedMessageSize = int.MaxValue,
            };
        }

        /// <summary>
        /// Implement this partial method to configure the service endpoint.
        /// </summary>
        /// <param name="serviceEndpoint">The endpoint to configure</param>
        /// <param name="clientCredentials">The client credentials</param>
        static partial void ConfigureEndpoint(ServiceEndpoint serviceEndpoint, ClientCredentials clientCredentials);
    }
}