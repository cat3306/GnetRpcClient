using System;
using System.Net;
using System.Net.Sockets;

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

        ConcurrentItemQueue<ArraySegment<byte>> sendQueue;
        ConcurrentItemQueue<Context> revQueue;

        private readonly object _streamLock = new object();
        public Client(Config config, Action<Context> onData, Action onDisconnected, Action onConnected)
        {
            this.config = config;
            sendQueue = new ConcurrentItemQueue<ArraySegment<byte>>(config.SendQueueCap);
            revQueue = new ConcurrentItemQueue<Context>(config.ReceiveQueueCap);
            OnData = onData;
            OnDisconnected = onDisconnected;
            OnConnected = onConnected;
        }

        public void Connect()
        {
            tcpClient = new TcpClient(AddressFamily.InterNetwork);
            tcpClient.NoDelay = config.NoDelay;
            tcpClient.SendTimeout = config.SendTimeout;
            tcpClient.ReceiveTimeout = config.ReceiveTimeout;
            tcpClient.SendBufferSize = config.SendBufferSize;
            tcpClient.BeginConnect(config.Ip, config.Port, (asyncResult) =>
            {
                if (tcpClient.Connected)
                {
                    tcpClient.EndConnect(asyncResult);
                    OnConnected.Invoke();
                }
                else
                {
                    Connect();
                }
            }, null);
        }

        public void Call(SerializeType serializeType, string servicePath, string method, Dictionary<string, string> metaDict, object obj)
        {
            if (tcpClient == null)
            {
                Log.Error("TcpClient is null");
                return;
            }
            if (!Connected)
            {
                Log.Error("Not connected");
                return;
            }
            var buffer = Protocol.Object2STream(serializeType, servicePath, method, metaDict, obj);
            var stream = tcpClient.GetStream();
            if (stream == null)
            {
                Log.Error("Stream is null");
                return;
            }
            stream.Write(buffer, 0, buffer.Length);
            var ctx = Protocol.Stream2Context(stream);
            OnData?.Invoke(ctx);
        }
        public void Send(ArraySegment<byte> buffer)
        {
            if (Connected && buffer.Array != null)
            {
                var stream = tcpClient?.GetStream();
                stream?.Write(buffer.Array, 0, buffer.Count);
                var ctx = Protocol.Stream2Context(stream);
                OnData?.Invoke(ctx);

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