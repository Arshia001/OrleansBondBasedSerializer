using Bond;
using Bond.IO.Unsafe;
using Bond.Protocols;
using Bond.Tag;
using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OrleansBondUtils
{
    [Schema]
    public class SerializationGrainReference
    {
        static ConcurrentDictionary<string, string> GenericArgsToTagCache = new ConcurrentDictionary<string, string>();
        static ConcurrentDictionary<string, string> TagToGenericArgsCache = new ConcurrentDictionary<string, string>(); //??

        // Least ugly hack possible into the Orleans GrainId system: we parse the
        // ToKeyString and store the info separately, then put that info back into
        // KeyString form and pass it to FromKeyString
        private const string GRAIN_REFERENCE_STR = "GrainReference";
        private const string SYSTEM_TARGET_STR = "SystemTarget";
        private const string OBSERVER_ID_STR = "ObserverId";
        private const string GENERIC_ARGUMENTS_STR = "GenericArguments";


        static void ParseGrainId(string GrainIdStr, out ulong n0, out ulong n1, out ulong typeCodeData, out string keyExt)
        {
            var trimmed = GrainIdStr.Trim();

            var fields = trimmed.Split(new char[] { '+' }, 2);
            n0 = ulong.Parse(fields[0].Substring(0, 16), NumberStyles.HexNumber);
            n1 = ulong.Parse(fields[0].Substring(16, 16), NumberStyles.HexNumber);
            typeCodeData = ulong.Parse(fields[0].Substring(32, 16), NumberStyles.HexNumber);
            keyExt = null;
            switch (fields.Length)
            {
                default:
                    throw new InvalidDataException("UniqueKey hex strings cannot contain more than one + separator.");
                case 1:
                    break;
                case 2:
                    keyExt = fields[1];
                    break;
            }
        }

        static string ParseAndConvertGenericArgsString(string genericArgs)
        {
            if (GenericArgsToTagCache.TryGetValue(genericArgs, out var CachedResult))
                return CachedResult;

            StringBuilder Result = new StringBuilder();

            int start = 0, idx, stack = 0;
            bool inside = false;
            for (idx = 0; idx < genericArgs.Length; ++idx)
            {
                if (inside)
                {
                    switch (genericArgs[idx])
                    {
                        case '[':
                            ++stack;
                            break;
                        case ']':
                            --stack;
                            if (stack == 0)
                            {
                                var typeName = genericArgs.Substring(start, idx - start - 1);
                                var type = Type.GetType(typeName);
                                if (Result.Length > 0)
                                    Result.Append(',');
                                Result.Append(BondSerializationUtil.GetTypeIdentifierString(type));

                                inside = false;
                            }
                            break;
                    }
                }
                else
                {
                    if (genericArgs[idx] == '[')
                    {
                        inside = true;
                        stack = 1;
                        start = idx + 1;
                    }
                }
            }

            GenericArgsToTagCache.TryAdd(genericArgs, Result.ToString());
            TagToGenericArgsCache.TryAdd(Result.ToString(), genericArgs);

            return Result.ToString();
        }

        public static ArraySegment<byte> ToByteArray(GrainReference GrainRef)
        {
            var KeyString = GrainRef.ToKeyString();
            var Ref = new SerializationGrainReference(0);

            // Parse grain reference key string
            string grainIdStr;
            int grainIdIndex = (GRAIN_REFERENCE_STR + "=").Length;

            int genericIndex = KeyString.IndexOf(GENERIC_ARGUMENTS_STR + "=", StringComparison.Ordinal);
            int observerIndex = KeyString.IndexOf(OBSERVER_ID_STR + "=", StringComparison.Ordinal);
            int systemTargetIndex = KeyString.IndexOf(SYSTEM_TARGET_STR + "=", StringComparison.Ordinal);

            if (genericIndex >= 0)
            {
                grainIdStr = KeyString.Substring(grainIdIndex, genericIndex - grainIdIndex).Trim();

                string genericStr = KeyString.Substring(genericIndex + (GENERIC_ARGUMENTS_STR + "=").Length);
                if (!String.IsNullOrEmpty(genericStr))
                    Ref.GenericArgs = ParseAndConvertGenericArgsString(genericStr);
            }
            else if (observerIndex >= 0)
            {
                grainIdStr = KeyString.Substring(grainIdIndex, observerIndex - grainIdIndex).Trim();

                string observerIdStr = KeyString.Substring(observerIndex + (OBSERVER_ID_STR + "=").Length);
                Ref.ObserverReferenceId = Guid.Parse(observerIdStr);
            }
            else if (systemTargetIndex >= 0)
            {
                grainIdStr = KeyString.Substring(grainIdIndex, systemTargetIndex - grainIdIndex).Trim();

                string systemTargetStr = KeyString.Substring(systemTargetIndex + (SYSTEM_TARGET_STR + "=").Length);
                Ref.SystemTargetSiloAddress = systemTargetStr;
            }
            else
            {
                grainIdStr = KeyString.Substring(grainIdIndex);
            }

            ParseGrainId(grainIdStr, out ulong n0, out ulong n1, out ulong typeCode, out string keyExt);
            Ref.IdN0 = n0;
            Ref.IdN1 = n1;
            Ref.TypeCodeData = typeCode;
            Ref.KeyExt = keyExt;

            return BondSerializer.Serialize(Ref);
        }

        static string ConvertBackToGenericArgsString(string Tags)
        {
            if (TagToGenericArgsCache.TryGetValue(Tags, out var CachedResult))
                return CachedResult;

            var Args = Tags.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            var Result = new StringBuilder();

            bool First = true;
            foreach (var Arg in Args)
            {
                if (First)
                    First = false;
                else
                    Result.Append(',');

                Result.Append('[');
                var Type = BondSerializationUtil.ParseTypeIdentifierString(Arg);
                Result.Append(Type.AssemblyQualifiedName);
                Result.Append(']');
            }

            TagToGenericArgsCache.TryAdd(Tags, Result.ToString());
            GenericArgsToTagCache.TryAdd(Result.ToString(), Tags);

            return Result.ToString();
        }

        public static GrainReference FromByteArray(ArraySegment<byte> Data, IGrainReferenceConverter GrainReferenceConverter)
        {
            if (Data.Count == 0)
                return null; // I actually think this never happens, because Bond simply leaves fields with default values out

            var Ref = (SerializationGrainReference)BondSerializer.Deserialize(typeof(SerializationGrainReference), new ArraySegmentReaderStream(Data));

            var s = new StringBuilder();
            s.AppendFormat("{0:x16}{1:x16}{2:x16}", Ref.IdN0, Ref.IdN1, Ref.TypeCodeData);
            if (Ref.KeyExt != null)
            {
                s.Append("+");
                s.Append(Ref.KeyExt);
            }
            string IdStr = s.ToString();

            string KeyString;
            if (Ref.ObserverReferenceId != null)
            {
                KeyString = String.Format("{0}={1} {2}={3}", GRAIN_REFERENCE_STR, IdStr, OBSERVER_ID_STR, Ref.ObserverReferenceId.ToString());
            }
            else if (Ref.SystemTargetSiloAddress != null)
            {
                KeyString = String.Format("{0}={1} {2}={3}", GRAIN_REFERENCE_STR, IdStr, SYSTEM_TARGET_STR, Ref.SystemTargetSiloAddress);
            }
            else if (Ref.GenericArgs != null)
            {
                KeyString = String.Format("{0}={1} {2}={3}", GRAIN_REFERENCE_STR, IdStr, GENERIC_ARGUMENTS_STR, ConvertBackToGenericArgsString(Ref.GenericArgs));
            }
            else
            {
                KeyString = String.Format("{0}={1}", GRAIN_REFERENCE_STR, IdStr);
            }

            return GrainReferenceConverter.GetGrainFromKeyString(KeyString);
        }


        private SerializationGrainReference(int i) { }

        [Obsolete("Used only by Bond, do not use")]
        public SerializationGrainReference() { }


        [Id(0)]
        public ulong IdN0 { get; set; }

        [Id(1)]
        public ulong IdN1 { get; set; }

        [Id(2)]
        public ulong TypeCodeData { get; set; }

        [Id(3), Type(typeof(nullable<string>))]
        public string KeyExt { get; set; }

        [Id(4), Type(typeof(nullable<string>))]
        public string GenericArgs { get; set; }

        [Id(5)]
        public Guid? ObserverReferenceId { get; set; }

        [Id(6), Type(typeof(nullable<string>))]
        public string SystemTargetSiloAddress { get; set; }
    }
}
