using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Reactive;
using System.Reactive.Linq;

namespace SimpleTCP_Example
{
    class Program
    {
        static SimpleTCP.Reactive.SimpleReactiveTcpServer server;
        static SimpleTCP.Reactive.SimpleReactiveTcpClient client = new
         SimpleTCP.Reactive.SimpleReactiveTcpClient();
        static async Task Main(string[] args)
        {
            server = new SimpleTCP.Reactive.SimpleReactiveTcpServer();

            server.ClientConnected.Subscribe(x =>
            {
                Console.WriteLine(x.Client.RemoteEndPoint + " Connected");
            });
            server.Start(2003);


            await Task.Delay(1000);
            ConnectTo(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2003));


            Console.ReadLine();
        }

        public static void ConnectTo(IPEndPoint endPoint)
        {
            client.Connect(endPoint.Address.ToString(), endPoint.Port);
        }

    }
}
