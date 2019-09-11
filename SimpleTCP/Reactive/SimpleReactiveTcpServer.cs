
using SimpleTCP.Reactive.Server;
using SimpleTCP.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using UniRx;
namespace SimpleTCP.Reactive
{
    public class SimpleReactiveTcpServer
    {
        public SimpleReactiveTcpServer()
        {
            StringEncoder = System.Text.Encoding.UTF8;
            SubscribeListeners();
        }

        private ReactiveCollection<ReactiveServerListener> _listeners = new ReactiveCollection<ReactiveServerListener>();
        public byte Delimiter { get { return DelimiterSubject.Value; } set { DelimiterSubject.OnNext(value); } }
        public BehaviorSubject<byte> DelimiterSubject { get; } = new BehaviorSubject<byte>(0x13);
        public System.Text.Encoding StringEncoder { get; set; }
        public bool AutoTrimStrings { get; set; }

        public IObservable<TcpClient> ClientConnected => _ClientConnected;
        Subject<TcpClient> _ClientConnected = new Subject<TcpClient>();
        public IObservable<TcpClient> ClientDisconnected => _ClientDisconnected;
        Subject<TcpClient> _ClientDisconnected = new Subject<TcpClient>();
        public IObservable<Message> DelimiterDataReceived => _DelimiterDataReceived;
        Subject<Message> _DelimiterDataReceived = new Subject<Message>();
        public IObservable<Message> DataReceived => _DataReceived;
        Subject<Message> _DataReceived = new Subject<Message>();

        Message CreateMessage(TcpClient client, byte[] msg)
            => new Message(msg, client, StringEncoder, DelimiterSubject.Value, AutoTrimStrings);
        void SubscribeListeners()
        {
            Dictionary<ReactiveServerListener, CompositeDisposable> disposables
              = new Dictionary<ReactiveServerListener, CompositeDisposable>();
            _listeners.ObserveAdd().Subscribe(x =>
            {
                var z = disposables.CreateIfNotExist(x.Value);
                x.Value.ClientConnected.Subscribe(y => _ClientConnected.OnNext(y)).ComposeTo(z);
                x.Value.ClientDisconnected.Subscribe(y => _ClientDisconnected.OnNext(y)).ComposeTo(z);
                x.Value.DelimiterMessage.Subscribe(y =>
                _DelimiterDataReceived.OnNext(CreateMessage(y.Item1, y.Item2))).ComposeTo(z);
                x.Value.EndTransmission.Subscribe(y =>
               _DataReceived.OnNext(CreateMessage(y.Item1, y.Item2))).ComposeTo(z);
            });
            _listeners.ObserveReset().Subscribe(x =>
            {
                foreach (var item in disposables) item.Value.Dispose();
            });
            _listeners.ObserveRemove().Subscribe(x =>
            {
                if (disposables.TryGetValue(x.Value, out var composit))
                {
                    disposables.Remove(x.Value);
                    composit.Dispose();
                }
            });
        }
        public IEnumerable<IPAddress> GetIPAddresses()
        {

            List<IPAddress> ipAddresses = new List<IPAddress>();
            IEnumerable<NetworkInterface> enabledNetInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up);
            foreach (NetworkInterface netInterface in enabledNetInterfaces)
            {
                IPInterfaceProperties ipProps = netInterface.GetIPProperties();
                foreach (UnicastIPAddressInformation addr in ipProps.UnicastAddresses)
                {
                    if (!ipAddresses.Contains(addr.Address))
                    {
                        ipAddresses.Add(addr.Address);
                    }
                }
            }

            var ipSorted = ipAddresses.OrderByDescending(ip => RankIpAddress(ip)).ToList();
            return ipSorted;
        }

        public List<IPAddress> GetListeningIPs()
        {
            List<IPAddress> listenIps = new List<IPAddress>();
            foreach (var l in _listeners)
            {
                if (!listenIps.Contains(l.IPAddress))
                {
                    listenIps.Add(l.IPAddress);
                }
            }

            return listenIps.OrderByDescending(ip => RankIpAddress(ip)).ToList();
        }

        public void Broadcast(byte[] data)
        {
            foreach (var client in _listeners.SelectMany(x => x.ConnectedClients))
            {
                client.GetStream().Write(data, 0, data.Length);
            }
        }

        public void Broadcast(string data)
        {
            if (data == null) { return; }
            Broadcast(StringEncoder.GetBytes(data));
        }

        public void BroadcastLine(string data)
        {
            if (string.IsNullOrEmpty(data)) { return; }
            if (data.LastOrDefault() != DelimiterSubject.Value)
            {
                Broadcast(data + StringEncoder.GetString(new byte[] { DelimiterSubject.Value }));
            }
            else
            {
                Broadcast(data);
            }
        }

        private int RankIpAddress(IPAddress addr)
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
                    var ipProps = nic.GetIPProperties();
                    if (ipProps.GatewayAddresses.Any())
                    {
                        if (ipProps.UnicastAddresses.Any(u => u.Address.Equals(addr)))
                        {
                            // if the preferred NIC has multiple addresses, boost all equally
                            // (justifies not bothering to differentiate... IOW YAGNI)
                            rankScore += 1000;
                        }

                        // only considering the first NIC that is UP and has a gateway defined
                        break;
                    }
                }
            }

            return rankScore;
        }

        private static IEnumerable<NetworkInterface> TryGetCurrentNetworkInterfaces()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.OperationalStatus == OperationalStatus.Up);
            }
            catch (NetworkInformationException)
            {
                return Enumerable.Empty<NetworkInterface>();
            }
        }

        public SimpleReactiveTcpServer Start(int port, bool ignoreNicsWithOccupiedPorts = true)
        {
            var ipSorted = GetIPAddresses();
            bool anyNicFailed = false;
            foreach (var ipAddr in ipSorted)
            {
                try
                {
                    Start(ipAddr, port);
                }
                catch (SocketException ex)
                {
                    DebugInfo(ex.ToString());
                    anyNicFailed = true;
                }
            }

            if (!IsStarted)
                throw new InvalidOperationException("Port was already occupied for all network interfaces");

            if (anyNicFailed && !ignoreNicsWithOccupiedPorts)
            {
                Stop();
                throw new InvalidOperationException("Port was already occupied for one or more network interfaces.");
            }

            return this;
        }

        public SimpleReactiveTcpServer Start(int port, AddressFamily addressFamilyFilter)
        {
            var ipSorted = GetIPAddresses().Where(ip => ip.AddressFamily == addressFamilyFilter);
            foreach (var ipAddr in ipSorted)
            {
                try
                {
                    Start(ipAddr, port);
                }
                catch { }
            }

            return this;
        }

        public bool IsStarted { get { return _listeners.Any(l => l.Listener.Active); } }

        public SimpleReactiveTcpServer Start(IPAddress ipAddress, int port)
        {
            Server.ReactiveServerListener listener = new Server.ReactiveServerListener(ipAddress, port, DelimiterSubject);
            _listeners.Add(listener);

            return this;
        }

        public void Stop()
        {
            foreach (var item in _listeners)
            {
                item.Stop();
            }

            while (_listeners.Any(l => l.Listener.Active))
            {
                Thread.Sleep(100);
            };
            _listeners.Clear();
        }

        public int ConnectedClientsCount
        {
            get
            {
                return _listeners.Sum(l => l.ConnectedClientsCount);
            }
        }

        #region Debug logging

        [System.Diagnostics.Conditional("DEBUG")]
        void DebugInfo(string format, params object[] args)
        {
            if (_debugInfoTime == null)
            {
                _debugInfoTime = new System.Diagnostics.Stopwatch();
                _debugInfoTime.Start();
            }
            System.Diagnostics.Debug.WriteLine(_debugInfoTime.ElapsedMilliseconds + ": " + format, args);
        }
        System.Diagnostics.Stopwatch _debugInfoTime;

        #endregion Debug logging
    }
}
