using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading;
using System;
using System.Reactive.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Threading.Tasks;
using SimpleTCP.Server;

namespace SimpleTCP.Reactive.Server
{
    internal class ReactiveServerListener
    {
        public IObservable<TcpClient> ClientDisconnected => _ClientDiconnected;
        public IObservable<TcpClient> ClientConnected => _ClientConnected;
        public IObservable<(TcpClient, byte[])> EndTransmission => _EndTransmission;
        public IObservable<(TcpClient, byte[])> DelimiterMessage => _DelimiterMessage;

        public int ConnectedClientsCount => _connectedClients.Count;
        public IEnumerable<TcpClient> ConnectedClients => _connectedClients;

        Subject<TcpClient> _ClientConnected { get; } = new Subject<TcpClient>();
        Subject<TcpClient> _ClientDiconnected { get; } = new Subject<TcpClient>();
        Subject<(TcpClient, byte[])> _EndTransmission { get; } = new Subject<(TcpClient, byte[])>();
        Subject<(TcpClient, byte[])> _DelimiterMessage { get; } = new Subject<(TcpClient, byte[])>();
        bool QueueStop { get; set; }
        internal IPAddress IPAddress { get; private set; }
        internal int Port { get; private set; }
        internal TimeSpan ReadLoopIntervalMs { get; set; }
        internal TcpListenerEx Listener { get; } = null;

        private List<TcpClient> _connectedClients = new List<TcpClient>();
        private List<TcpClient> _disconnectedClients = new List<TcpClient>();
        private Dictionary<string, List<byte>> _clientBuffers = new Dictionary<string, List<byte>>();
        private BehaviorSubject<byte> _delimiter;



        internal ReactiveServerListener(IPAddress ipAddress, int port, BehaviorSubject<byte> delimeter)
        {
            QueueStop = false;
            _delimiter = delimeter;
            IPAddress = ipAddress;
            Port = port;
            ReadLoopIntervalMs = TimeSpan.FromMilliseconds(10);
            Listener = new TcpListenerEx(ipAddress, port);
            Start();
        }
        IDisposable listenerLoop;
        private void Start()
        {
            Listener.Start();
            listenerLoop = Observable.Interval(ReadLoopIntervalMs).SubscribeOn(TaskPoolScheduler.Default).Where(x => !QueueStop).Subscribe(x =>
              {
                  RunLoopStep();
              },
              e =>
              {
                  Console.WriteLine(e);
              });

        }
        public void Stop()
        {
            QueueStop = true;
            listenerLoop.Dispose();
            Listener.Stop();
        }

        bool IsSocketConnected(Socket s)
        {
            // https://stackoverflow.com/questions/2661764/how-to-check-if-a-socket-is-connected-disconnected-in-c
            bool part1 = s.Poll(1000, SelectMode.SelectRead);
            bool part2 = (s.Available == 0);
            if ((part1 && part2) || !s.Connected)
                return false;
            else
                return true;
        }

        private void RunLoopStep()
        {
            if (_disconnectedClients.Count > 0)
            {
                var disconnectedClients = _disconnectedClients.ToArray();
                _disconnectedClients.Clear();
                foreach (var disC in disconnectedClients)
                {
                    _connectedClients.Remove(disC);
                    _ClientDiconnected.OnNext(disC);
                }
            }
            if (Listener.Pending())
            {
                var newClient = Listener.AcceptTcpClient();
                _connectedClients.Add(newClient);
                _ClientConnected.OnNext(newClient);
            }
            foreach (var c in _connectedClients)
            {
                if (IsSocketConnected(c.Client) == false)
                {
                    _disconnectedClients.Add(c);
                }
                int bytesAvailable = c.Available;
                if (bytesAvailable == 0)
                {
                    //Thread.Sleep(10);
                    continue;
                }
                List<byte> bytesReceived = new List<byte>();
                while (c.Available > 0 && c.Connected)
                {
                    byte[] nextByte = new byte[1];
                    c.Client.Receive(nextByte, 0, 1, SocketFlags.None);
                    bytesReceived.AddRange(nextByte);

                    string clientKey = c.Client.RemoteEndPoint.ToString();
                    if (!_clientBuffers.ContainsKey(clientKey))
                    {
                        _clientBuffers.Add(clientKey, new List<byte>());
                    }
                    List<byte> clientBuffer = _clientBuffers[clientKey];
                    if (nextByte[0] == _delimiter.Value)
                    {
                        byte[] msg = clientBuffer.ToArray();
                        clientBuffer.Clear();
                        _DelimiterMessage.OnNext((c, msg));
                    }
                    else
                    {
                        clientBuffer.AddRange(nextByte);
                    }
                }
                if (bytesReceived.Count > 0)
                {
                    _EndTransmission.OnNext((c, bytesReceived.ToArray()));

                }
            }
        }
    }
}
