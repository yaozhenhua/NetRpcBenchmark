// <copyright file="NetWcfPerfServer.cs" company="Zhenhua Yao">
// Copyright (c) Zhenhua Yao. All rights reserved.
// </copyright>

namespace NetWcfPerf
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.ServiceModel;
    using System.ServiceModel.Description;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// WCF self hosting program
    /// </summary>
    internal sealed class NetWcfPerfServer
    {
        private const int DefaultWcfEchoPort = 12345;

        private static void Main(string[] args)
        {
            var port = args.Length > 0
                ? int.Parse(args[0])
                : 12345;

            var baseAddress = new Uri($"net.tcp://0.0.0.0:{port}");
            using (var host = new ServiceHost(typeof(WcfEchoImpl), baseAddress))
            {
                var smb = new ServiceMetadataBehavior();
                smb.HttpGetEnabled = true;
                smb.HttpGetUrl = new Uri($"http://localhost:{port + 1}");
                smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
                ////host.Description.Behaviors.Add(smb);

                host.Open();
                Console.WriteLine($"WCF Echo Service running at {baseAddress}. Press <Enter> to stop");
                Console.ReadLine();
                host.Close();
            }
        }
    }
}
