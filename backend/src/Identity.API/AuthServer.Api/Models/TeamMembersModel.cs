using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace AuthServer.Api.Models
{
    public class TeamMembersModel : DocumentBase
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
        public string UserId
        {
            get => _userId.ToString();
            set => _userId = ObjectId.Parse(value);
        }
        private ObjectId _userId;

        [BsonRepresentation(BsonType.ObjectId)]
        public string InvitedByUserId
        {
            get => _invitedByUserId.ToString();
            set => _invitedByUserId = ObjectId.Parse(value);
        }
        private ObjectId _invitedByUserId;

        public int MozScore { get; set; } = 0;

        public string[] Skills { get; set; } = [];
        public string ProfileSummary { get; set; }

        public string Role { get; set; } = "Member";

        /// <summary>
        /// Basis points (0..10000). 10000 = 100%.
        /// Metadata only; MozaiksPay is source of truth for wallets/ledger.
        /// </summary>
        public int MpAllocationBps { get; set; } = 0;

        public string? Note { get; set; }

        public int MemberStatus { get; set; } = 1;//0=Removed, 1=Member
    }
}
