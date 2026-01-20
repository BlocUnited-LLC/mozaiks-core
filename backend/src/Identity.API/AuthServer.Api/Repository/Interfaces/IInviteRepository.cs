using AuthServer.Api.Models;

namespace AuthServer.Api.Repository.Interfaces
{
    public interface IInviteRepository
    {
        Task<InviteModel> CreateInviteAsync(InviteModel invite);
        Task<InviteModel> GetInviteByIdAsync(string id);
        Task<IEnumerable<InviteModel>> GetSentInvitesAsync(string userId);
        Task<IEnumerable<InviteModel>> GetReceivedInvitesAsync(string userId);
        Task UpdateInviteAsync(InviteModel invite);
        long CheckDuplicateInvites(string invitedById, string receipentId, string appId);
        Task UpdateInviteStatusAsync(string inviteId, int isAccepted);
    }
}
