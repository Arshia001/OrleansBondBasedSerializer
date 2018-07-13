using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Orleans;
using Orleans.Providers;
using OrleansCassandraUtils.Persistence;
using Orleans.Runtime;

namespace OrleansBondUtils
{
    public class BondCassandraStorageSerializationProvider : IStorageSerializationProvider
    {
        public void Init(IProviderRuntime ProviderRuntime)
        {
            BondSerializationUtil.Initialize(ProviderRuntime.ServiceProvider);
        }

        public bool IsSupportedType(Type Type)
        {
            return BondSerializer.CheckSupportedTypeForTopLevelSerialization(Type);
        }

        public string GetTypeString(string OrleansTypeString, Type Type)
        {
            return BondSerializationUtil.GetTypeIdentifierString(Type);
        }

        public object Deserialize(Type ExpectedType, byte[] Data)
        {
            return BondSerializer.Deserialize(ExpectedType, new MemoryStream(Data));
        }

        public byte[] Serialize(object Object)
        {
            return BondSerializer.Serialize(Object).ToArray();
        }
    }
}
