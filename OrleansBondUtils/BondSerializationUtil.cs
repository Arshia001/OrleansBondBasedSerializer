using Bond;
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
    public static class BondSerializationUtil
    {
        static object LockObj = new object();
        static Dictionary<string, Type> TypeDictionary = new Dictionary<string, Type>();
        static Dictionary<RuntimeTypeHandle, string> TagDictionary = new Dictionary<RuntimeTypeHandle, string>();
        static ConcurrentDictionary<RuntimeTypeHandle, string> TypeIdStringCache = new ConcurrentDictionary<RuntimeTypeHandle, string>();
        static ConcurrentDictionary<string, Type> ReverseTypeIdStringCache = new ConcurrentDictionary<string, Type>();
        static Assembly MscorlibAssembly;
        static Assembly CurrentAssembly;

        public static void Initialize(IServiceProvider ServiceProvider)
        {
            lock (LockObj)
            {
                if (TypeDictionary.Any())
                    return;

                // TODO : search the codebase for other frequently used and create aliases for them
                TypeDictionary.Add("i", typeof(int));
                TypeDictionary.Add("ui", typeof(uint));
                TypeDictionary.Add("s", typeof(short));
                TypeDictionary.Add("us", typeof(ushort));
                TypeDictionary.Add("l", typeof(long));
                TypeDictionary.Add("ul", typeof(ulong));
                TypeDictionary.Add("f", typeof(float));
                TypeDictionary.Add("d", typeof(double));
                TypeDictionary.Add("c", typeof(char));
                TypeDictionary.Add("b", typeof(byte));
                TypeDictionary.Add("st", typeof(string));
                TypeDictionary.Add("ls", typeof(List<>));
                TypeDictionary.Add("dc", typeof(Dictionary<,>));
                TypeDictionary.Add("hs", typeof(HashSet<>));
                TypeDictionary.Add("sl", typeof(SortedList<,>));
                TypeDictionary.Add("sd", typeof(SortedDictionary<,>));
                TypeDictionary.Add("ss", typeof(SortedSet<>));
                TypeDictionary.Add("nl", typeof(Nullable<>));

                foreach (var KV in TypeDictionary)
                    TagDictionary.Add(KV.Value.TypeHandle, KV.Key);


                var MessageType = typeof(IGenericSerializable);
                foreach (var Assembly in AppDomain.CurrentDomain.GetAssemblies())
                    foreach (var Type in Assembly.DefinedTypes)
                    {
                        var TagAttribute = Type.GetCustomAttribute<BondSerializationTagAttribute>(false);
                        if (TagAttribute == null || string.IsNullOrEmpty(TagAttribute.Tag))
                            if (Type.IsSubclassOf(MessageType) && !Type.IsAbstract)
                                throw new FormatException($"Class {Type.FullName} implements {nameof(IGenericSerializable)} and must be tagged using BondSerializationTagAttribute with a non-empty tag");
                            else
                                continue;

                        if (TypeDictionary.TryGetValue(TagAttribute.Tag, out var ExistingType))
                            throw new FormatException($"Tag {TagAttribute.Tag} is duplicate between types {Type.FullName} and {ExistingType.FullName}");

                        TypeDictionary.Add(TagAttribute.Tag, Type);
                        TagDictionary.Add(Type.TypeHandle, TagAttribute.Tag);
                    }

                MscorlibAssembly = Assembly.GetAssembly(typeof(System.Object));
                CurrentAssembly = Assembly.GetExecutingAssembly();

                BondTypeAliasConverter.InitializeTypeMappings(ServiceProvider);
            }
        }

        static void GetTypeIdentifierString_Helper(Type Type, Assembly GenericAssembly, ref StringBuilder Result)
        {
            Type BaseType = Type;
            if (Type.IsGenericType)
                BaseType = Type.GetGenericTypeDefinition();

            if (TagDictionary.TryGetValue(BaseType.TypeHandle, out var Tag))
                Result.Append(Tag);
            else
            {
                if (Type.IsInterface && Type.GetInterfaces().Any(t => t == typeof(IGrain)))
                    throw new ArgumentException($"Type {Type.FullName} is a grain interface but has no tag");

                // For types is mscorlib or the current assembly, Type.GetType doesn't require an assembly name.
                // Additionally, it is likely that the generic type will reside in the same assembly as the parent
                // type. In those cases, we do not store assembly information to minimize storage overhead.
                var Asm = BaseType.Assembly;
                if (Asm == CurrentAssembly || Asm == MscorlibAssembly || Asm == GenericAssembly)
                    Result.Append('^').Append(BaseType.FullName);
                else
                    Result.Append('^').Append(BaseType.AssemblyQualifiedName);
            }

            if (Type.IsGenericType)
            {
                Result.Append('[');
                bool bFirst = true;
                foreach (var GenericArg in Type.GetGenericArguments())
                {
                    if (bFirst)
                        bFirst = false;
                    else
                        Result.Append(';');

                    GetTypeIdentifierString_Helper(GenericArg, GenericAssembly ?? Type.Assembly, ref Result);
                }
                Result.Append(']');
            }
        }

        public static string GetTypeIdentifierString(Type Type)
        {
            if (TypeIdStringCache.TryGetValue(Type.TypeHandle, out var Found))
                return Found;

            StringBuilder Result = new StringBuilder();

            GetTypeIdentifierString_Helper(Type, null, ref Result);

            var ResultString = Result.ToString();
            TypeIdStringCache.TryAdd(Type.TypeHandle, ResultString);
            return ResultString;
        }

        static Type ParseTypeIdentifierString_Helper(string TypeIdentifierString, ref int Position, Assembly GenericAssembly)
        {
            bool IsTypeName = false;
            if (TypeIdentifierString[Position] == '^')
            {
                ++Position;
                IsTypeName = true;
            }

            var End = TypeIdentifierString.IndexOfAny(new char[] { '[', ']', ';' }, Position);
            if (End == -1)
                End = TypeIdentifierString.Length;

            var TypeName = TypeIdentifierString.Substring(Position, End - Position);
            Position = End;

            Type Type = null;
            if (IsTypeName)
            {
                Type = Type.GetType(TypeName);

                // This handles the special case of types residing in the same assembly as
                // the generic, which I expect will be frequent
                if (Type == null)
                    Type = GenericAssembly.GetType(TypeName);
            }
            else
            {
                TypeDictionary.TryGetValue(TypeName, out Type);
            }

            if (Type == null)
                throw new TypeLoadException();

            List<Type> Generics = new List<Type>();
            if (Position < TypeIdentifierString.Length && TypeIdentifierString[Position] == '[')
            {
                if (GenericAssembly == null)
                    GenericAssembly = Type.Assembly;

                ++Position;
                while (true)
                {
                    Generics.Add(ParseTypeIdentifierString_Helper(TypeIdentifierString, ref Position, GenericAssembly));
                    var NextChar = TypeIdentifierString[Position++];
                    if (NextChar == ';')
                        continue;
                    else if (NextChar == ']')
                        break;
                    else
                        throw new FormatException();
                }
            }

            if (Type.IsGenericType)
            {
                if (Generics.Count > 0)
                    Type = Type.MakeGenericType(Generics.ToArray());
                else
                    throw new FormatException();
            }

            return Type;
        }

        public static Type ParseTypeIdentifierString(string TypeIdentifierString)
        {
            if (ReverseTypeIdStringCache.TryGetValue(TypeIdentifierString, out var Found))
                return Found;

            Type Result;
            int Position = 0;
            try
            {
                Result = ParseTypeIdentifierString_Helper(TypeIdentifierString, ref Position, null);
            }
            catch
            {
                return null;
            }

            ReverseTypeIdStringCache.TryAdd(TypeIdentifierString, Result);
            return Result;
        }
    }
}
