using MongoDB.Bson.Serialization.Conventions;

namespace Insights.API.Shared;

public static class MongoConventions
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        var pack = new ConventionPack
        {
            new IgnoreExtraElementsConvention(true),
            new CamelCaseElementNameConvention()
        };

        ConventionRegistry.Register("mozaiks_insights_conventions", pack, _ => true);
    }
}
