namespace GnetRpcClient
{
    public interface ICodec
    {
        T Deserialize<T>(byte[] data);
        byte[] Serialize(object obj);

        T Deserialize<T>(ArraySegment<byte> data);
    }
}