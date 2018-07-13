# OrleansBondBasedSerializer

A serializer for MSR Orleans based on Bond. The `Bond/cs` folder contains a *modified* version of Bond's C# module. The `OrleansBondUtils` folder contains the source for the serializer. The code is meant only to demonstrate what such a serializer might look like, and it doesn't include a Visual Studio solution/project. You don't want to use this code with Bond's IDL. Instead, data classes should be created by hand. 

Take a look at `BondTypeAliasConverter` for the changes to Bond (they're mostly focused on facilitating serialization of different types without additional boilerplate code). Take a look at `SerializationGrainReference`, `SerializationGenericReference` and `Sample` for usage samples.

Worth noting, the serializer can do type-safe serialization of any nested object, as long as it's a Bond schema. See `Sample`.
