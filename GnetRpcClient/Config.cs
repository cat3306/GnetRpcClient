namespace GnetRpcClient
{
    public class Config
    {

        public string? Ip { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 7898;

        public bool NoDelay = true;

        public int SendTimeout { get; set; } = 5000;
        public int ReceiveTimeout { get; set; } = 5000;

        public int SendBufferSize { get; set; } = 1024 * 1024;
        public int ReceiveBufferSize { get; set; } = 1024 * 1024;

        public int SendQueueCap { get; set; } = 1000_00;
        public int ReceiveQueueCap { get; set; } = 1000_00;
    }
}