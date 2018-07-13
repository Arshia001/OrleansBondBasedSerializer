using Bond;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansBondUtils
{
    [Schema]
    class SerializationGenericReference
    {
        // TODO: It would be AWESOME if we could just store this class directly, but
        // unfortunately, Bond's type aliases allow only primitive types to be aliased.
        // We should check to see if we can add support for custom types in Bond's source,
        // but for now, we're forced to do a double serialization here.
        public static ArraySegment<byte> ToByteArray(IGenericSerializable Serializable)
        {
            if (Serializable == null)
                return default(ArraySegment<byte>);

            var Type = Serializable.GetType();
            var Result = new SerializationGenericReference(0)
            {
                Type = BondSerializationUtil.GetTypeIdentifierString(Type),
                Data = BondSerializer.Serialize(Serializable)
            };

            return BondSerializer.Serialize(Result);
        }

        public static IGenericSerializable FromByteArray(ArraySegment<byte> Data)
        {
            if (Data.Count == 0)
                return null;

            var Ref = (SerializationGenericReference)BondSerializer.Deserialize(typeof(SerializationGenericReference), new ArraySegmentReaderStream(Data));
            var Type = BondSerializationUtil.ParseTypeIdentifierString(Ref.Type);
            return (IGenericSerializable)BondSerializer.Deserialize(Type, new MemoryStream(Ref.Data.ToArray()));
        }


        private SerializationGenericReference(int i) { }

        [Obsolete("Used only by Bond, do not use")]
        public SerializationGenericReference() { }

        [Id(0)]
        public string Type { get; set; }

        [Id(1)]
        public ArraySegment<byte> Data { get; set; }
    }
}
