using Bond;
using Bond.Tag;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Providers;
using Orleans.Runtime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OrleansBondUtils
{
    public class BondTypeAliasConverter
    {
        static IGrainReferenceConverter GrainReferenceConverter;
        static MethodInfo AsReferenceMethodInfo;
        static ConcurrentDictionary<Type, MethodInfo> CachedAsReferenceConcreteMethods = new ConcurrentDictionary<Type, MethodInfo>();

        public static void InitializeTypeMappings(IServiceProvider ServiceProvider)
        {
            GrainReferenceConverter = ServiceProvider.GetRequiredService<IGrainReferenceConverter>();
            AsReferenceMethodInfo = typeof(GrainExtensions).GetMethod("AsReference", new Type[] { typeof(IAddressable) });

            CustomTypeRegistry.AddTypeMapping(typeof(DateTime), typeof(long), false);
            CustomTypeRegistry.AddTypeMapping(typeof(DateTime?), typeof(long?), false);
            CustomTypeRegistry.AddTypeMapping(typeof(DateTime[]), typeof(long[]), false);
            CustomTypeRegistry.AddTypeMapping(typeof(IAddressable), typeof(nullable<blob>), true);
            CustomTypeRegistry.AddTypeMapping(typeof(Guid?), typeof(nullable<blob>), false);
            CustomTypeRegistry.AddTypeMapping(typeof(Guid), typeof(blob), false);
            CustomTypeRegistry.AddTypeMapping(typeof(Guid[]), typeof(blob[]), false);

            // Automatically resolve IGenericSerializable fields or fields with GenericSerializationAttribute to blobs
            CustomTypeRegistry.RegisterResolutionCallback(ResolveSchemaField);

            // Globally register this as a type converter for all types
            CustomTypeRegistry.RegisterTypeConverter(typeof(BondTypeAliasConverter));
        }

        public static Type ResolveSchemaField(ISchemaField Field)
        {
            if (Field.MemberType == typeof(IGenericSerializable) ||
                (Field.MemberType.IsSubclassOf(typeof(IGenericSerializable)) && Field.MemberInfo.GetCustomAttributes(typeof(GenericSerializationAttribute), false) != null))
                return typeof(nullable<blob>);

            return null;
        }


        public static long Convert(DateTime value, long unused, Type ExpectedType)
        {
            return value.Ticks;
        }

        public static DateTime Convert(long value, DateTime unused, Type ExpectedType)
        {
            return new DateTime(value);
        }


        public static long? Convert(DateTime? value, long? unused, Type ExpectedType)
        {
            return value?.Ticks;
        }

        public static DateTime? Convert(long? value, DateTime? unused, Type ExpectedType)
        {
            return value.HasValue ? new DateTime(value.Value) : default(DateTime?);
        }


        public static ArraySegment<byte> Convert(IAddressable value, ArraySegment<byte> unused, Type ExpectedType)
        {
            // Orleans has an AsWeaklyTypedReference method to convert any IAddressable into a GrainReference, but it's internal...
            return SerializationGrainReference.ToByteArray(value as GrainReference ?? throw new NotSupportedException("Only GrainReference types are allowed for all serializable IAddressables"));
        }

        static MethodInfo GetAsReferenceMethodInfo(Type GrainType)
        {
            if (!CachedAsReferenceConcreteMethods.TryGetValue(GrainType, out var Result))
            {
                Result = AsReferenceMethodInfo.MakeGenericMethod(new Type[] { GrainType });
                CachedAsReferenceConcreteMethods.TryAdd(GrainType, Result);
            }

            return Result;
        }

        public static IAddressable Convert(ArraySegment<byte> value, IAddressable unused, Type ExpectedType)
        {
            return (IAddressable)GetAsReferenceMethodInfo(ExpectedType).Invoke(null, new object[] { SerializationGrainReference.FromByteArray(value, GrainReferenceConverter) });
        }


        public static ArraySegment<byte> Convert(IGenericSerializable value, ArraySegment<byte> unused, Type ExpectedType)
        {
            return SerializationGenericReference.ToByteArray(value);
        }

        public static IGenericSerializable Convert(ArraySegment<byte> value, IGenericSerializable unused, Type ExpectedType)
        {
            return SerializationGenericReference.FromByteArray(value);
        }


        public static ArraySegment<byte> Convert(Guid value, ArraySegment<byte> unused, Type ExpectedType)
        {
            return new ArraySegment<byte>(value.ToByteArray());
        }

        public static Guid Convert(ArraySegment<byte> value, Guid unused, Type ExpectedType)
        {
            return new Guid(value.ToArray());
        }


        public static ArraySegment<byte> Convert(Guid? value, ArraySegment<byte> unused, Type ExpectedType)
        {
            if (value == null)
                return new ArraySegment<byte>();
            return new ArraySegment<byte>(value.Value.ToByteArray());
        }

        public static Guid? Convert(ArraySegment<byte> value, Guid? unused, Type ExpectedType)
        {
            if (value.Count == 0)
                return null;
            return new Guid(value.ToArray());
        }
    }
}
