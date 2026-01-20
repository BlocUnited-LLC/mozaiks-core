using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using MongoDB.Driver;

namespace AuthServer.Api.Repository
{
    public class InviteRepository: IInviteRepository
    {
        private readonly IMongoCollection<InviteModel> _inviteCollection;

        public InviteRepository(IMongoDatabase database)
        {
            _inviteCollection = database.GetCollection<InviteModel>("Invites");
        }

        public async Task<InviteModel> CreateInviteAsync(InviteModel invite)
        {
            await _inviteCollection.InsertOneAsync(invite);
            return invite;
        }

        public async Task<InviteModel> GetInviteByIdAsync(string id)
        {
            return await _inviteCollection.Find(i => i.Id == id).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<InviteModel>> GetSentInvitesAsync(string userId)
        {
            return await _inviteCollection.Find(i => i.InvitedByUserId == userId && i.InviteStatus > 0).ToListAsync();
        }
        public async Task<IEnumerable<InviteModel>> GetReceivedInvitesAsync(string userId)
        {
            return await _inviteCollection.Find(i => i.ReceipentUserId == userId && i.InviteStatus > 0).ToListAsync();
        }

        public async Task UpdateInviteAsync(InviteModel invite)
        {
            await _inviteCollection.ReplaceOneAsync(i => i.Id == invite.Id, invite);
        }

        public async Task UpdateInviteStatusAsync(string inviteId, int status)
        {
            var filter = Builders<InviteModel>.Filter.Eq(i => i.Id, inviteId);
            var update = Builders<InviteModel>.Update
                .Set(i => i.InviteStatus, status)
                .Set(i => i.UpdatedAt, DateTime.UtcNow);
            await _inviteCollection.UpdateOneAsync(filter, update);
        }
        public long CheckDuplicateInvites(string invitedById, string receipentId, string appId)
        {
            // Only treat "Pending" as a duplicate. Accepted/Rejected invites are historical.
            return _inviteCollection.Find(i =>
                    i.AppId == appId &&
                    i.InvitedByUserId == invitedById &&
                    i.ReceipentUserId == receipentId &&
                    i.InviteStatus == 1)
                .CountDocuments();
        }
    }
}
