
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
using SimpleTCP.Platform.Windows;
using SimpleTCP.Platform;
namespace SimpleTCP.Reactive
{
    public class SimpleReactiveTcpServer
    {
        IPlatformNetworkInfoProvider Provider { get; }
        public SimpleReactiveTcpServer()
        {
            Provider = new WindowsNetworkInfoProvider();
            StringEncoder = System.Text.Encoding.UTF8;
            SubscribeListeners();
        }
        public SimpleReactiveTcpServer(IPlatformNetworkInfoProvider provider)
        {
            Provider = provider;
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

            return listenIps.OrderByDescending(ip => Provider.RankIpAddress(ip)).ToList();
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



        public SimpleReactiveTcpServer Start(int port, bool ignoreNicsWithOccupiedPorts = true)
        {
            var ipSorted = Provider.GetIPAddresses();
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
            var ipSorted = Provider.GetIPAddresses().Where(ip => ip.AddressFamily == addressFamilyFilter);
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
