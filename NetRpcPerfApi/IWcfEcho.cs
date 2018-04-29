// <copyright file="IWcfEcho.cs" company="Zhenhua Yao">
// Copyright (c) Zhenhua Yao. All rights reserved.
// </copyright>

namespace NetRpcPerf
{
    using System.ServiceModel;
    using System.Threading.Tasks;

    /// <summary>
    /// WCF service contract for Echo
    /// </summary>
    [ServiceContract]
    public interface IWcfEcho
    {
        /// <summary>
        /// Async version of echo
        /// </summary>
        /// <param name="msg">Input message</param>
        /// <returns>Task object to resolve to an identical as input</returns>
        [OperationContract(Action = "http://tempuri.org/IWcfEcho/Echo", ReplyAction = "http://tempuri.org/IWcfEcho/EchoResponse")]
        Task<string> EchoAsync(string msg);
    }
}
