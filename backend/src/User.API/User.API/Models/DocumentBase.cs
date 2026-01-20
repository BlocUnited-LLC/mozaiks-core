using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.Text.Json.Serialization;

namespace User.API.Models
{
    public class DocumentBase
    {
        [JsonIgnore]
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("_id")]
        //[BsonId(IdGenerator = typeof(StringObjectIdGenerator))]

        public string? Id { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
    }
}
