using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SimpleTCP.Platform
{
    public interface IPlatformNetworkInfoProvider
    {
        IEnumerable<IPAddress> GetIPAddresses();
        int RankIpAddress(IPAddress addr);
    }
}
