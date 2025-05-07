namespace GnetRpcClient
{
    struct CallReq
    {
        public int a { set; get; }
        public int b { set; get; }
    }
    struct CallResp
    {
        public int C { set; get; }
    }
    public class Program
    {

        public static void OnData(Context ctx)
        {
            ctx.MetaData.TryGetValue(Share.RESPONSE_STATUS_KEY, out var code);
            if (code == Share.RESPONSE_STATUS_KEY_OK)
            {
                var rsp = ctx.DeserializePayload<CallResp>();
                Console.WriteLine("Received: " + rsp.C);
                Protocol.ReturnContext(ctx);
            }
            else
            {
                if (ctx.MetaData.TryGetValue(Share.RESPONSE_ERROR_MSG_KEY, out var err))
                {

                    Console.WriteLine("Error: " + err);
                }
            }
        }
        public static void Main(string[] args)
        {

            var client = new Client(new Config
            {
                Ip = "127.0.0.1",
                Port = 7898,
                NoDelay = true,
                SendTimeout = 5000,
                ReceiveTimeout = 5000,
                SendBufferSize = 1024 * 1024,
                ReceiveBufferSize = 1024 * 1024,
                SendQueueCap = 1000_00,
                ReceiveQueueCap = 1000_00
            }, OnData, () => { }, () => { });

            // var client = new Client(Protocol.MaxBufferCap, () => { }, OnData, () => { });
            client.Connect();
            int i = 0;
            while (true)
            {
                i++;
                client.Call(SerializeType.CodeJson, "Builtin", "Add", new Dictionary<string, string>(), new CallReq { a = i, b = 2 });
            }
        }
    }
}