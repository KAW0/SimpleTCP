using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleTCP.Reactive;
using System.Net.NetworkInformation;

namespace SimpleTCP.Tests.Reactive
{

    [TestClass]
    public class ReactiveServerTests : IDisposable
    {
        readonly int _serverPort = 8911;
        readonly SimpleReactiveTcpServer _server;

        public ReactiveServerTests()
        {
            _server = new SimpleReactiveTcpServer().Start(_serverPort);
        }

        public void Dispose()
        {
            if (_server.IsStarted)
                _server.Stop();
        }

        [TestMethod]
        public void Listening_port_opens_and_closes_when_server_starts_and_stops()
        {
            Assert.IsTrue(IsTcpPortListening(_serverPort), "Tcp port should be open when server has started.");
            _server.Stop();
            Assert.IsTrue(!IsTcpPortListening(_serverPort), "Tcp port should be closed when server has stopped.");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Start_fails_if_all_nics_are_occupied()
        {
            var server2 = new SimpleReactiveTcpServer().Start(_serverPort);
            server2.Stop(); //Guard-clause. Should never reach this.
        }



        public static bool IsTcpPortListening(int port)
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Where(x => x.Port == port)
                .Any();
        }
    }
}
