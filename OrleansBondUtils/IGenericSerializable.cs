using Bond;
using Orleans;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OrleansBondUtils
{
    /// <summary>
    /// This is a marker interface used to identify types which will be serialized to database.
    /// 
    /// All 'top-level'* serialized types must, in addition to specifying a valid Bond schema via Bond's
    /// many attributes, implement this type and also be tagged using BondSerializationTagAttribute.
    /// Additionally, if general polymorphism is required, a field of type IGenericSerializable
    /// must be declared. This field will automatically be converted to a type-preserving representation 
    /// which can then be deserialiazed regardless of actual type. This obviously works only for schema types.
    /// 
    /// * 'Top-level' means types that will be serialized to database, or, alternatively, grain state types.
    /// </summary>
    public interface IGenericSerializable
    {
    }
}
