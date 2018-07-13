using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansBondUtils
{
    /// <summary>
    /// Tag used to identify types when loading from database. Tags MUST NEVER BE CHANGED FOR ANY TYPE.
    /// Tagging an as-yet-untagged type is supported by the serialization engine, but WILL cause trouble
    /// with database keys. Care should be taken to tag all types. Specifically, grain states and 
    /// interfaces should always be tagged.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class BondSerializationTagAttribute : Attribute
    {
        public string Tag { get; }

        public BondSerializationTagAttribute(string Tag)
        {
            this.Tag = Tag;
        }
    }
}
