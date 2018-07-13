using Bond;
using Orleans.Runtime;
using Orleans.Serialization;
using System;
using System.Collections.Concurrent;
using System.Reflection;

using TBondReader = Bond.Protocols.CompactBinaryReader<Bond.IO.Unsafe.InputStream>;
using TBondWriter = Bond.Protocols.CompactBinaryWriter<Bond.IO.Unsafe.OutputBuffer>;
using TBondDeserializer = Bond.Deserializer<Bond.Protocols.CompactBinaryReader<Bond.IO.Unsafe.InputStream>>;
using TBondSerializer = Bond.Serializer<Bond.Protocols.CompactBinaryWriter<Bond.IO.Unsafe.OutputBuffer>>;
using Bond.IO.Unsafe;
using System.Linq;
using System.IO;

namespace OrleansBondUtils
{
    public static class BondSerializer
    {
        private static ConcurrentDictionary<RuntimeTypeHandle, TBondSerializer> SerializerCache = new ConcurrentDictionary<RuntimeTypeHandle, TBondSerializer>();
        private static ConcurrentDictionary<RuntimeTypeHandle, TBondDeserializer> DeserializerCache = new ConcurrentDictionary<RuntimeTypeHandle, TBondDeserializer>();


        static Tuple<TBondSerializer, TBondDeserializer> Register(Type Type)
        {
            try
            {
#if DEBUG
                var TypeName = Type.Name;
#endif
                if (Type.GetTypeInfo().GetCustomAttribute<SchemaAttribute>() == null)
                    return new Tuple<TBondSerializer, TBondDeserializer>(null, null);

                var Handle = Type.TypeHandle;

                var Result = new Tuple<TBondSerializer, TBondDeserializer>(new TBondSerializer(Type), new TBondDeserializer(Type));

                SerializerCache.TryAdd(Handle, Result.Item1);
                DeserializerCache.TryAdd(Handle, Result.Item2);

                return Result;
            }
            catch
            {
                return null;
            }
        }

        public static bool CheckSupportedTypeForTopLevelSerialization(Type ItemType)
        {
            if (DeserializerCache.ContainsKey(ItemType.TypeHandle))
                return true;

            var TypeInfo = ItemType.GetTypeInfo();
            if (TypeInfo.IsGenericType && ItemType.IsConstructedGenericType == false)
                return false;

            if (!TypeInfo.GetInterfaces().Any(t => t == typeof(IGenericSerializable)))
                return false;

            var RegisterResult = Register(ItemType);
            return RegisterResult != null;
        }

        public static object Deserialize(Type ExpectedType, Stream Data)
        {
            if (!DeserializerCache.TryGetValue(ExpectedType.TypeHandle, out var Deserializer))
            {
                Deserializer = Register(ExpectedType).Item2;
                if (Deserializer == null)
                    throw new ArgumentOutOfRangeException($"Type {ExpectedType.FullName} cannot be deserialized");
            }

            var Reader = new TBondReader(new InputStream(Data, 1024));
            var Result = Deserializer.Deserialize(Reader);

            var IODH = Result as IOnDeserializedHandler;
            if (IODH != null)
                IODH.OnDeserialized();

            return Result;
        }

        public static ArraySegment<byte> Serialize(object Item)
        {
            if (Item == null)
                return default(ArraySegment<byte>);

            var Type = Item.GetType();
            if (!SerializerCache.TryGetValue(Type.TypeHandle, out var Serializer))
            {
                Serializer = Register(Type).Item1;
                if (Serializer == null)
                    throw new ArgumentOutOfRangeException($"Type {Type.FullName} cannot be serialized");
            }

            var Buffer = new OutputBuffer(4096);
            var Writer = new TBondWriter(Buffer);
            Serializer.Serialize(Item, Writer);
            return Buffer.Data;
        }
    }
}
