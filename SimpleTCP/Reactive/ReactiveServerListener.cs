using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading;
using System;
using System.Reactive.Linq;
namespace SimpleTCP.Server
{
    internal class ReactiveServerListener
    {
        private List<TcpClient> _connectedClients = new List<TcpClient>();
        private List<TcpClient> _disconnectedClients = new List<TcpClient>();
        private SimpleReactiveTcpServer _parent = null;
        private Dictionary<string, List<byte>> _clientBuffers = new Dictionary<string, List<byte>>();
        private byte _delimiter = 0x13;
        private Thread _rxThread = null;
        Subject<TcpClient> _ClientConnected { get; } = new Subject<TcpClient>();
        Subject<TcpClient> _ClientDiconnected { get; } = new Subject<TcpClient>();
        Subject<(TcpClient, byte[])> _EndTransmission { get; } = new Subject<(TcpClient, byte[])>();
        Subject<(TcpClient, byte[])> _DelimiterMessage { get; } = new Subject<(TcpClient, byte[])>();
        public IObservable<TcpClient> ClientDiconnected => _ClientDiconnected;
        public IObservable<TcpClient> ClientConnected => _ClientConnected;
        public IObservable<(TcpClient, byte[])> EndTransmission => _EndTransmission;
        public IObservable<(TcpClient, byte[])> DelimiterMessage => _DelimiterMessage;

        public int ConnectedClientsCount
        {
            get { return _connectedClients.Count; }
        }

        public IEnumerable<TcpClient> ConnectedClients { get { return _connectedClients; } }

        internal ReactiveServerListener(SimpleReactiveTcpServer parentServer, IPAddress ipAddress, int port)
        {
            QueueStop = false;
            _parent = parentServer;
            IPAddress = ipAddress;
            Port = port;
            ReadLoopIntervalMs = 10;

            Listener = new TcpListenerEx(ipAddress, port);
            Start();
        }

        private void Start()
        {
            Listener.Start();

            System.Threading.ThreadPool.QueueUserWorkItem(ListenerLoop);
        }

        private void StartThread()
        {
            if (_rxThread != null) { return; }
            _rxThread = new Thread(ListenerLoop) { IsBackground = true };
            _rxThread.Start();
        }

        internal bool QueueStop { get; set; }
        internal IPAddress IPAddress { get; private set; }
        internal int Port { get; private set; }
        internal int ReadLoopIntervalMs { get; set; }

        internal TcpListenerEx Listener { get; } = null;



        private void ListenerLoop(object state)
        {
            while (!QueueStop)
            {
                try
                {
                    RunLoopStep();
                }
                catch
                {

                }

                Thread.Sleep(ReadLoopIntervalMs);
            }
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

            _delimiter = _parent.Delimiter;

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
                    if (nextByte[0] == _delimiter)
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
