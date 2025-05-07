using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Net.Sockets;
namespace GnetRpcClient
{
    // header(4 byte)+msgSeq(8 byte)+pathMethodLen(4 byte)+metaDataLen(4 byte)+payloadLen(4 byte)
    // path@method(n byte)+metaData(n byte)+payload(n byte)
    public enum SerializeType : byte
    {
        CodeNone = 0,
        String,
        CodeJson,
        ProtoBuf,
    }
    public struct Header
    {
        public byte MagicNumber;
        public SerializeType SerializeType;
        public byte Version;
        public byte Heartbeat;
    }

    public class Context
    {
        public Header Header;
        public ArraySegment<byte> Payload;
        public Dictionary<string, string>? MetaData;
        public string? ServicePath;
        public string? ServiceMethod;
        public ulong MsgSeq;
        public T DeserializePayload<T>()
        {
            var codec = Protocol.Codec(Header.SerializeType);
            var t = codec.Deserialize<T>(Payload);
            return t;
        }
        public void SerializePayload<T>(T obj)
        {
            if (obj == null) { return; }
            var codec = Protocol.Codec(Header.SerializeType);
            Payload = codec.Serialize(obj);
        }
        public void Reset()
        {
            if (MetaData != null)
                MetaData.Clear();
            Payload = default;
            ServicePath = string.Empty;
            ServiceMethod = string.Empty;
            MsgSeq = 0;

        }
    }

    public class Protocol
    {
        static readonly int headerLen = 4;
        static readonly int msgSeqLen = 8;
        static readonly int pathMethodLen = 4;
        static readonly int metaDataLen = 4;
        static readonly int payloadLen = 4;
        public static readonly int MaxBufferCap = 1 << 24; // 16MB
        static readonly byte MagicNumber = 0xFF;
        static readonly byte version = 0x01;
        static readonly int fixedLen = headerLen + pathMethodLen + msgSeqLen + metaDataLen + payloadLen;
        static readonly Pool<Context> contextPool = new Pool<Context>(() =>
        {
            return new Context
            {
                Header = new Header { },
                Payload = default,
                MetaData = new Dictionary<string, string>(),
                ServicePath = string.Empty,
                ServiceMethod = string.Empty,
                MsgSeq = 0,
            };
        });
        public static void ReturnContext(Context ctx)
        {
            ctx.Reset();
            contextPool.Return(ctx);
        }
        public static Context TakeContext()
        {
            return contextPool.Take();
        }
        public static Dictionary<SerializeType, ICodec> Codecs = new Dictionary<SerializeType, ICodec>()
        {
            {SerializeType.CodeJson, new JsonCodec()}
        };
        public static ICodec Codec(SerializeType serializeType)
        {

            if (Codecs.TryGetValue(serializeType, out ICodec? codec))
            {
                return codec;
            }
            else
            {
                return new JsonCodec();
            }
        }
        public static string JoinServiceMethod(string path, string method)
        {
            return path + "@" + method;
        }
        public static byte[] Object2STream(SerializeType serializeType, string servicePath, string method, Dictionary<string, string> metaDict, object obj)
        {
            var ctx = contextPool.Take();

            ctx.Header.MagicNumber = MagicNumber;
            ctx.Header.Version = version;
            ctx.Header.SerializeType = serializeType;
            ctx.ServiceMethod = method;
            ctx.ServicePath = servicePath;
            ctx.MetaData = metaDict;
            var coder = Codec(ctx.Header.SerializeType);
            var payload = coder.Serialize(obj);
            var metaData = Array.Empty<byte>();
            var methodStr = JoinServiceMethod(ctx.ServicePath, ctx.ServiceMethod);
            if (ctx.MetaData != null && ctx.MetaData.Count > 0)
            {
                var jsonCoder = Codec(SerializeType.CodeJson);
                metaData = jsonCoder.Serialize(ctx.MetaData);
            }
            var totalLength = fixedLen + (uint)methodStr.Length + (uint)metaData.Length + (uint)payload.Length;
            var totalBuffer = new byte[totalLength];
            // encode header
            totalBuffer[0] = ctx.Header.MagicNumber;
            totalBuffer[1] = ctx.Header.Version;
            totalBuffer[2] = ctx.Header.Heartbeat;
            totalBuffer[3] = (byte)ctx.Header.SerializeType;
            // encode header

            // encode msgSeq
            ulong msgSeq = 0;
            var msgSeqBytes = BitConverter.GetBytes(msgSeq);
            Buffer.BlockCopy(msgSeqBytes, 0, totalBuffer, headerLen, msgSeqLen);
            // encode msgSeq

            // encode pathMethodLen
            var methodLenBuffer = BitConverter.GetBytes(methodStr.Length);
            Buffer.BlockCopy(methodLenBuffer, 0, totalBuffer, headerLen + msgSeqLen, pathMethodLen);
            // encode pathMethodLen

            // encode metaDataLen
            var metaDataLenBuffer = BitConverter.GetBytes(metaData.Length);
            Buffer.BlockCopy(metaDataLenBuffer, 0, totalBuffer, headerLen + msgSeqLen + pathMethodLen, metaDataLen);
            // encode metaDataLen

            // encode payloadLen
            var payloadLenBuffer = BitConverter.GetBytes(payload.Length);
            Buffer.BlockCopy(payloadLenBuffer, 0, totalBuffer, headerLen + msgSeqLen + pathMethodLen + metaDataLen, payloadLen);
            // encode payloadLen

            // encode pathMethod
            var methodBytes = Encoding.UTF8.GetBytes(methodStr);

            Buffer.BlockCopy(methodBytes, 0, totalBuffer, fixedLen, methodBytes.Length);
            // encode pathMethod



            if (metaData.Length > 0)
            {
                // encode metaData
                Buffer.BlockCopy(metaData, 0, totalBuffer, fixedLen + methodBytes.Length, metaData.Length);
            }

            // encode payload
            Buffer.BlockCopy(payload, 0, totalBuffer, fixedLen + methodBytes.Length + metaData.Length, payload.Length);
            // encode payload

            return totalBuffer;
        }


        public static Context Stream2Context(NetworkStream stream)
        {

            try
            {
                while (true)
                {
                    var fixBuffer = new byte[fixedLen];
                    if (!stream.ReadExactly(fixBuffer, fixBuffer.Length))
                    {
                        continue;
                    }
                    var magic = fixBuffer[0];
                    var version = fixBuffer[1];
                    var heartbeat = fixBuffer[2];
                    var serializeType = (SerializeType)fixBuffer[3];

                    var msgSeq = BitConverter.ToUInt64(fixBuffer, headerLen);
                    var pathMethodLength = BitConverter.ToInt32(fixBuffer, headerLen + msgSeqLen);
                    var metaDataLength = BitConverter.ToInt32(fixBuffer, headerLen + msgSeqLen + pathMethodLen);
                    var payloadLength = BitConverter.ToInt32(fixBuffer, headerLen + msgSeqLen + pathMethodLen + metaDataLen);

                    var dataBuffer = new byte[pathMethodLength + metaDataLength + payloadLength];
                    if (!stream.ReadExactly(dataBuffer, dataBuffer.Length))
                    {
                        continue;
                    }
                    var data = new ArraySegment<byte>(dataBuffer);
                    var methodStr = Encoding.UTF8.GetString(data[..pathMethodLength]);
                    var pathAndMethod = methodStr.Split("@");
                    if (pathAndMethod.Length != 2)
                    {
                        return null;
                    }
                    var metaData = new Dictionary<string, string>();
                    if (metaDataLength > 0)
                    {
                        var jsonCoder = Codec(SerializeType.CodeJson);
                        metaData = jsonCoder.Deserialize<Dictionary<string, string>>(data[pathMethodLength..(pathMethodLength + metaDataLength)]);
                    }
                    var payload = data[(pathMethodLength + metaDataLength)..];
                    var ctx = new Context
                    {
                        Header = new Header
                        {
                            MagicNumber = magic,
                            SerializeType = serializeType,
                            Version = version,
                            Heartbeat = heartbeat,
                        },
                        Payload = payload,
                        MetaData = metaData,
                        ServicePath = pathAndMethod[0],
                        ServiceMethod = pathAndMethod[1],
                        MsgSeq = msgSeq,
                    };
                    return ctx;
                }

            }
            catch (Exception exception)
            {
                // something went wrong. the thread was interrupted or the
                // connection closed or we closed our own connection or ...
                // -> either way we should stop gracefully
                Log.Info("[Telepathy] ReceiveLoop: finished receive function for connectionId=" + " reason: " + exception);
                return null;
            }
        }
    }
}