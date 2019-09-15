using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Android.App;
using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Net;
using Java.Util;
using SimpleTCP.Platform;

namespace SimpleTCP.Android
{
    public class AndroidNetworkInfoProvider : IPlatformNetworkInfoProvider
    {
        ConnectivityManager ConnectivityManager { get; }
        WifiManager WifiManger { get; }
        public AndroidNetworkInfoProvider()
        {
            ConnectivityManager = (ConnectivityManager)Application.Context.GetSystemService(Context.ConnectivityService);
            WifiManger = (WifiManager)Application.Context.GetSystemService(Service.WifiService);

        }
        public int RankIpAddress(IPAddress addr)
        {
            int rankScore = 1000;

            if (IPAddress.IsLoopback(addr))
            {
                // rank loopback below others, even though their routing metrics may be better
                rankScore = 300;
            }
            else if (addr.AddressFamily == AddressFamily.InterNetwork)
            {
                rankScore += 100;
                // except...
                if (addr.GetAddressBytes().Take(2).SequenceEqual(new byte[] { 169, 254 }))
                {
                    // APIPA generated address - no router or DHCP server - to the bottom of the pile
                    rankScore = 0;
                }
            }
            if (rankScore > 500)
            {
                foreach (var nic in TryGetCurrentNetworkInterfaces())
                {
                    //.Address.HostAddress
                    //      var ipProps =nic.InetAddresses // GetSystemService(context. //nic..GetIPProperties();
                    //      if (ipProps.GatewayAddresses.Any())
                    //      {
                    //          if (ipProps.UnicastAddresses.Any(u => u.Address.Equals(addr)))
                    //          {
                    //              // if the preferred NIC has multiple addresses, boost all //equally
                    //              // (justifies not bothering to differentiate... IOW YAGNI)
                    //              rankScore += 1000;
                    //          }
                    //          // only considering the first NIC that is UP and has a gateway //defined
                    //          break;
                    //     }
                }
            }
            return 0;
            //return rankScore;
        }

        public IEnumerable<NetworkInterface> TryGetCurrentNetworkInterfaces()
        {
            try
            {
                return FromJavaEnumeration<NetworkInterface>(NetworkInterface.NetworkInterfaces).Where(x => x.IsUp);
            }
            catch
            {
                return Enumerable.Empty<NetworkInterface>();
            }
        }
        public IEnumerable<IPAddress> GetIPAddresses()
        {
            List<IPAddress> ipAddresses = new List<IPAddress>();
            var networkIntefaces = Collections.List(Java.Net.NetworkInterface.NetworkInterfaces);
            foreach (var item in networkIntefaces)
            {
                var AddressInterface = (item as Java.Net.NetworkInterface).InterfaceAddresses;
                foreach (var AInterface in AddressInterface)
                {
                    if (AInterface.Broadcast != null)
                        if (IPAddress.TryParse(AInterface.Address.HostAddress, out var addres))
                            ipAddresses.Add(addres);
                }
            }
            var ipSorted = ipAddresses.OrderByDescending(ip => RankIpAddress(ip)).ToList();
            return ipSorted;
        }
        static IEnumerable<T> FromJavaEnumeration<T>(Java.Util.IEnumeration enumeration) where T : class, IJavaObject
        {
            while (enumeration.HasMoreElements)
            {
                T next;
                try
                {
                    next = enumeration.NextElement().JavaCast<T>();

                }
                catch
                {
                    yield break;
                }
                yield return next;
            }
        }
    }
}
