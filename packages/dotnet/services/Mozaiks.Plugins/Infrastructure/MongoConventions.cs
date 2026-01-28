using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Plugins.API.Infrastructure;

public static class MongoConventions
{
    private static bool _registered = false;

    public static void Register()
    {
        if (_registered) return;

        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true),
            new EnumRepresentationConvention(BsonType.String)
        };

        ConventionRegistry.Register("PluginsApiConventions", pack, _ => true);

        _registered = true;
    }
}
