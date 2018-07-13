[Schema, BondSerializationTag("t1")]
public class Type1 : IGenericSerializable, IOnDeserializedHandler
{
    [Id(0)]
    public int SomeData { get; set; }

    [Id(1)]
    public string MoreData { get; set; }

    [Id(2)]
    public DateTime ADate { get; set; } // DateTime serialization is supported in BondTypeAliasConverter without any additional attributes/boilerplate.

    [Id(3)]
    public IGenericSerializable SomeObject { get; set; }


    public void OnDeserialized()
    {
        // An instance was deserialized, perform any initialization tasks here
    }
}

[Schema, BondSerializationTag("t2")]
public class Type2: IGenericSerializable
{
    [Id(0)]
    public int Data { get; set; }
}


// this is a valid serialization scenario.
// The Type2 reference will be correctly serialized along with its type.
// On deserialization, the type will be restored and you will end up with
// a Type2 reference.
var x = new Type1();
x.SomeObject = new Type2();
BondSerializer.Serialize(x); 
