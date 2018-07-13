using OrleansCassandraUtils.Reminders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans.Runtime;

namespace OrleansBondUtils
{
    public class BondGrainReferenceConversionProvider : IGrainReferenceConversionProvider
    {
        IGrainReferenceConverter GrainReferenceConverter;


        public BondGrainReferenceConversionProvider(IGrainReferenceConverter GrainReferenceConverter)
        {
            this.GrainReferenceConverter = GrainReferenceConverter;
        }

        public GrainReference GetGrain(byte[] Key)
        {
            return SerializationGrainReference.FromByteArray(new ArraySegment<byte>(Key), GrainReferenceConverter);
        }

        public byte[] GetKey(GrainReference GrainRef)
        {
            return SerializationGrainReference.ToByteArray(GrainRef).ToArray();
        }
    }
}
