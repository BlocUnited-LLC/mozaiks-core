using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthServer.Api.Models
{
    public class UserSettingsModel : DocumentBase
    {
        [BsonElement("userId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("timezone")]
        public string Timezone { get; set; } = "UTC";

        [BsonElement("notifyDeployments")]
        public bool NotifyDeployments { get; set; } = true;

        [BsonElement("notifyErrors")]
        public bool NotifyErrors { get; set; } = true;

        [BsonElement("notifyFundingUpdates")]
        public bool NotifyFundingUpdates { get; set; } = true;
    }
}

