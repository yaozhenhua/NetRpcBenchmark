// <copyright file="WcfEchoImpl.cs" company="Zhenhua Yao">
// Copyright (c) Zhenhua Yao. All rights reserved.
// </copyright>

namespace NetWcfPerf
{
    using System.ServiceModel;
    using System.Threading.Tasks;
    using NetRpcPerf;

    /// <summary>
    /// WCF service implementation of Echo
    /// </summary>
    [ServiceBehavior(
        AddressFilterMode = AddressFilterMode.Any,
        ConcurrencyMode = ConcurrencyMode.Multiple,
        InstanceContextMode = InstanceContextMode.Single,
        IncludeExceptionDetailInFaults = false,
        EnsureOrderedDispatch = false,
        IgnoreExtensionDataObject = true,
        UseSynchronizationContext = false)]
    public sealed class WcfEchoImpl : IWcfEcho
    {
        /// <inheritdoc />
        public Task<string> EchoAsync(string msg) => Task.FromResult(msg);
    }
}
