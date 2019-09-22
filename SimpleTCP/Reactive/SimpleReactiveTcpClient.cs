using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Reactive.Concurrency;
using System.Threading.Tasks;

namespace SimpleTCP.Reactive
{
    public class SimpleReactiveTcpClient : IDisposable
    {
        Subject<(TcpClient, byte[])> _DataReceived { get; } = new Subject<(TcpClient, byte[])>();
        Subject<(TcpClient, byte[])> _DelimiterDataReceived { get; } = new Subject<(TcpClient, byte[])>();
        public IObservable<Message> DataReceived => _DataReceived.Select(y => CreateMessage(y.Item1, y.Item2));
        public IObservable<Message> DelimiterDataReceived =>
            _DelimiterDataReceived.Select(y => CreateMessage(y.Item1, y.Item2));
        private List<byte> _queuedMsg = new List<byte>();
        public byte Delimiter { get; set; }
        public System.Text.Encoding StringEncoder { get; set; }
        public bool AutoTrimStrings { get; set; }
        public TcpClient TcpClient { get; private set; } = null;
        internal bool QueueStop { get; set; }
        internal TimeSpan ReadLoopIntervalMs { get; set; } = TimeSpan.FromMilliseconds(10);
        IDisposable ClientLoop { get; set; }
        Message CreateMessage(TcpClient client, byte[] msg)
          => new Message(msg, client, StringEncoder, Delimiter, AutoTrimStrings);

        public SimpleReactiveTcpClient()
        {
            StringEncoder = System.Text.Encoding.UTF8;
            Delimiter = 0x13;
        }
        public SimpleReactiveTcpClient Connect(string hostNameOrIpAddress, int port)
        {
            if (string.IsNullOrEmpty(hostNameOrIpAddress))
            {
                throw new ArgumentNullException("hostNameOrIpAddress");
            }
            TcpClient = new TcpClient();
            TcpClient.Connect(hostNameOrIpAddress, port);
            ClientLoop = Observable.Interval(ReadLoopIntervalMs)
                .SubscribeOn(ThreadPoolScheduler.Instance)
                .Where(x => TcpClient != null && TcpClient.Connected)
                .Where(x => !QueueStop).Subscribe(x =>
             {
                 RunLoopStep();
             });
            return this;
        }

        public SimpleReactiveTcpClient Disconnect()
        {
            ClientLoop.Dispose();
            if (TcpClient == null) { return this; }
            TcpClient.Close();
            TcpClient = null;
            return this;
        }
        private void RunLoopStep()
        {
            var c = TcpClient;
            int bytesAvailable = c.Available;
            if (bytesAvailable == 0)
            {
                Thread.Sleep(10);// Task.Delay(10);
                return;
            }

            var delimiter = this.Delimiter;
            List<byte> bytesReceived = new List<byte>();

            while (c.Available > 0 && c.Connected)
            {
                byte[] nextByte = new byte[1];
                c.Client.Receive(nextByte, 0, 1, SocketFlags.None);
                bytesReceived.AddRange(nextByte);
                if (nextByte[0] == delimiter)
                {
                    byte[] msg = _queuedMsg.ToArray();
                    _queuedMsg.Clear();
                    _DelimiterDataReceived.OnNext((c, msg));
                }
                else
                {
                    _queuedMsg.AddRange(nextByte);
                }
            }
            if (bytesReceived.Count > 0)
            {
                _DataReceived.OnNext((c, bytesReceived.ToArray()));
            }
        }



        public void Write(byte[] data)
        {
            if (TcpClient == null) { throw new Exception("Cannot send data to a null TcpClient (check to see if Connect was called)"); }
            TcpClient.GetStream().Write(data, 0, data.Length);
        }

        public void Write(string data)
        {
            if (data == null) { return; }
            Write(StringEncoder.GetBytes(data));
        }

        public void WriteLine(string data)
        {
            if (string.IsNullOrEmpty(data)) { return; }
            if (data.LastOrDefault() != Delimiter)
            {
                Write(data + StringEncoder.GetString(new byte[] { Delimiter }));
            }
            else
            {
                Write(data);
            }
        }

        public IObservable<Message> WriteLineAndGetReply(string data, TimeSpan timeout)
        {
            AsyncSubject<Message> asyncSubject = new AsyncSubject<Message>();
            var z = new CompositeDisposable();
            WriteLine(data);
            z.Add(DataReceived.Timeout(timeout).Subscribe(y =>
            {
                asyncSubject.OnNext(y);
                asyncSubject.OnCompleted();
                z.Dispose();
            }, e => asyncSubject.OnError(e)));
            return asyncSubject;
        }


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                ClientLoop.Dispose();
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).

                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                QueueStop = true;
                if (TcpClient != null)
                {
                    try
                    {
                        TcpClient.Close();
                    }
                    catch { }
                    TcpClient = null;
                }

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~SimpleTcpClient() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
