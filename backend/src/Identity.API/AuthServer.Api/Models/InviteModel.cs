using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace AuthServer.Api.Models
{
    public class InviteModel : DocumentBase
    {       
        [BsonElement("appId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string AppId
        {
            get => _appId.ToString();
            set => _appId = ObjectId.Parse(value);
        }
        private ObjectId _appId;

        [BsonRepresentation(BsonType.ObjectId)]
        public string ReceipentUserId
        {
            get => _receipentUserId.ToString();
            set => _receipentUserId = ObjectId.Parse(value);
        }
        private ObjectId _receipentUserId;

        [BsonRepresentation(BsonType.ObjectId)]
        public string InvitedByUserId
        {
            get => _invitedByUserId.ToString();
            set => _invitedByUserId = ObjectId.Parse(value);
        }
        private ObjectId _invitedByUserId;
       
        public required string SenderEmail { get; set; }
        public required string ReceiverEmail { get; set; }
        public required string SenderUserName { get; set; }

        [BsonElement("appName")]
        public required string AppName { get; set; }
        public required string InvitationMessage { get; set; }

        /// <summary>
        /// Proposed team role for the recipient (metadata).
        /// </summary>
        public string? ProposedRole { get; set; }

        /// <summary>
        /// Proposed MP allocation in basis points (0..10000). Metadata only.
        /// </summary>
        public int? ProposedMpAllocationBps { get; set; }

        public string? ProposedNote { get; set; }
        public int InviteStatus { get; set; } = 1;//1=Pending, 2=Accepted, 3=Rejected,0=Deleted
    }
}
