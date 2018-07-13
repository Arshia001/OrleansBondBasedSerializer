using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansBondUtils
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    class GenericSerializationAttribute : Attribute { }
}
