using Newtonsoft.Json;
using System.Text;
namespace GnetRpcClient
{
    public class JsonCodec : ICodec
    {
        public T Deserialize<T>(byte[] data)
        {
            var str = Encoding.UTF8.GetString(data);
            if (string.IsNullOrEmpty(str))
            {
                return default;
            }
            T t = JsonConvert.DeserializeObject<T>(str);
            return t;
        }

        public T Deserialize<T>(ArraySegment<byte> data)
        {
            var str = Encoding.UTF8.GetString(data);
            if (string.IsNullOrEmpty(str))
            {
                return default;
            }
            T t = JsonConvert.DeserializeObject<T>(str);
            return t;
        }

        public byte[] Serialize(object obj)
        {
            var str = JsonConvert.SerializeObject(obj);
            var bytes = Encoding.UTF8.GetBytes(str);
            return bytes;
        }

        public override string ToString()
        {
            return "json";
        }
    }
}