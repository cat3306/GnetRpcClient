using System;
using System.Net;
using System.Net.Sockets;
using Serilog;
using Serilog.Core;


namespace GnetRpcClient
{
    public class Client
    {
        public TcpClient? tcpClient;

        public Action OnConnected;
        public Action<Context> OnData;
        public Action OnDisconnected;
        public Config config;


        public bool Connected => tcpClient != null &&
                                       tcpClient.Client != null &&
                                       tcpClient.Client.Connected;


        Thread sendThread;
        Thread revThread;
        Thread reconnectThread;

        ConcurrentItemQueue<ArraySegment<byte>> sendQueue;
        ConcurrentItemQueue<Context> revQueue;

        int TryCount = 0;
        public ManualResetEvent sendPending = new ManualResetEvent(false);
        Log log = new Log();
        public Client(Config config, Action<Context> onData, Action onDisconnected, Action onConnected)
        {

            this.config = config;
            sendQueue = new ConcurrentItemQueue<ArraySegment<byte>>(config.SendQueueCap);
            revQueue = new ConcurrentItemQueue<Context>(config.ReceiveQueueCap);
            OnData = onData;
            OnDisconnected = onDisconnected;
            OnConnected = onConnected;
            tcpClient = new TcpClient(AddressFamily.InterNetwork);
            tcpClient.NoDelay = config.NoDelay;
            tcpClient.SendTimeout = config.SendTimeout;
            tcpClient.ReceiveTimeout = config.ReceiveTimeout;
            tcpClient.SendBufferSize = config.SendBufferSize;

            reconnectThread = new Thread(TryConnect)
            {
                IsBackground = true
            };
            reconnectThread.Start();

            sendThread = new Thread(SendLoop)
            {
                IsBackground = true
            };
            sendThread.Start();



        }
        void RevLoop()
        {
            var stream = tcpClient.GetStream();
            while (Connected)
            {
                var ctx = Protocol.Stream2Context(stream);
          
                revQueue.Enqueue(ctx);
            }

        }
        public void Tick(int loop)
        {
            for (int i = 0; i < loop; i++)
            {
                if (revQueue.Count > 0)
                {
                    var ctx = revQueue.Dequeue();
                    OnData.Invoke(ctx);
                }
            }
        }
        void SendLoop()
        {
            while (true)
            {
                if (!Connected) continue;
                sendPending.Reset();
                var buffer = sendQueue.Dequeue();
                try
                {
                    tcpClient.Client.Send(buffer);
                }
                catch (Exception ex)
                {
                    log.logger.Error("Send error: ", ex);
                }
                sendPending.WaitOne();
            }
        }
        void TryConnect()
        {
            while (true)
            {
                Thread.Sleep(config.CheckReconnectInterval);
                if (!Connected)
                {
                    TryCount++;
                    Connect();
                    log.logger.Information("Try connect " + TryCount);
                }
            }
        }
        public void Connect()
        {
            if (tcpClient == null || Connected) return;
            tcpClient.BeginConnect(config.Ip, config.Port, (asyncResult) =>
            {
                if (tcpClient.Connected)
                {
                    tcpClient.EndConnect(asyncResult);
                    OnConnected.Invoke();
                    log.logger.Information("Connected");
                    if (revThread == null)
                        revThread = new Thread(RevLoop)
                        {
                            IsBackground = true
                        };
                    revThread.Start();
                }
                else
                {
                    log.logger.Error("Connect failed");
                }
            }, null);
        }

        public void Call(SerializeType serializeType, string servicePath, string method, Dictionary<string, string> metaDict, object obj)
        {
            if (tcpClient == null)
            {
                log.logger.Error("TcpClient is null");
                return;
            }
            if (!Connected)
            {
                log.logger.Error("Not connected");
                return;
            }
            var buffer = Protocol.Object2STream(serializeType, servicePath, method, metaDict, obj);
            sendQueue.Enqueue(buffer);
            sendPending.Set();
        }
        public void Send(ArraySegment<byte> buffer)
        {
            if (Connected && buffer.Array != null)
            {
                sendQueue.Enqueue(buffer);
                sendPending.Set();
            }
        }


        public void Dispose()
        {
            // close client
            tcpClient?.Close();

            revThread?.Interrupt();

            tcpClient = null;
        }
    }
}