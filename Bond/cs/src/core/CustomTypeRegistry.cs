using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bond.Internal.Reflection;

namespace Bond
{
    public static class CustomTypeRegistry
    {
        static ReaderWriterLockSlim Lock = new ReaderWriterLockSlim();
        static Dictionary<Type, Type> TypeHandlers = new Dictionary<Type, Type>();
        static Dictionary<Type, Type> TypeHandlers_IncludeSubclasses = new Dictionary<Type, Type>();
        static List<Func<ISchemaField, Type>> TypeResolutionCallbacks = new List<Func<ISchemaField, Type>>();
        static HashSet<Type> GlobalConverters = new HashSet<Type>();


        public static void AddTypeMapping(Type FieldType, Type SchemaType, bool IncludeSubclasses)
        {
            try
            {
                Lock.EnterWriteLock();
                (IncludeSubclasses ? TypeHandlers_IncludeSubclasses : TypeHandlers).Add(FieldType, SchemaType);
            }
            catch (Exception)
            {
                throw new InvalidOperationException($"A type mapping is already registered for {FieldType}");
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        public static void RegisterResolutionCallback(Func<ISchemaField, Type> Callback)
        {
            try
            {
                Lock.EnterWriteLock();
                TypeResolutionCallbacks.Add(Callback);
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        internal static Type ResolveFieldType(ISchemaField Field)
        {
            try
            {
                Lock.EnterReadLock();
                Type Result;
                foreach (var Callback in TypeResolutionCallbacks)
                    if ((Result = Callback?.Invoke(Field)) != null)
                        return Result;
                return null;
            }
            finally
            {
                Lock.ExitReadLock();
            }

        }

        internal static Type GetTypeMapping(Type FieldType)
        {
            try
            {
                Lock.EnterReadLock();
                if (TypeHandlers.TryGetValue(FieldType, out var Result))
                    return Result;
                return TypeHandlers_IncludeSubclasses.Where(kv => kv.Key.IsAssignableFrom(FieldType) || FieldType.GetAllInterfaces().Any(t => t == kv.Key)).FirstOrDefault().Value;
            }
            finally
            {
                Lock.ExitReadLock();
            }
        }

        public static void RegisterTypeConverter(Type Type)
        {
            try
            {
                Lock.EnterWriteLock();
                GlobalConverters.Add(Type);
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        internal static IEnumerable<Type> GetTypeConverters()
        {
            try
            {
                Lock.EnterReadLock();
                return GlobalConverters.ToList();
            }
            finally
            {
                Lock.ExitReadLock();
            }
        }
    }
}
