using MongoDB.Bson.Serialization.Conventions;

namespace AuthServer.Api.Shared
{
    public static class MongoConventions
    {
        private static bool _registered;

        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            var pack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true)
            };

            ConventionRegistry.Register("mozaiks_conventions", pack, _ => true);
        }
    }
}

