using AuthServer.Api.Models;
using AuthServer.Api.Repository;
using AuthServer.Api.Repository.Interfaces;

namespace AuthServer.Api.Services
{
    public class InviteService
    {
        private readonly IInviteRepository _inviteRepository;
        private readonly IUserRepository _userRepository;
        public InviteService(IInviteRepository inviteRepository, IUserRepository userRepository)
        {
            _inviteRepository = inviteRepository;
            _userRepository = userRepository;
           
        }
        public async Task<InviteModel> CreateInviteAsync(InviteModel inviteModel)
        {
            var user = await _userRepository.GetUserByEmailAsync(inviteModel.SenderEmail);

            var result = await _inviteRepository.CreateInviteAsync(inviteModel);

            if (user != null)
            {
                await SendEmailInviteAsync(inviteModel.SenderEmail, inviteModel.InvitationMessage);
            }
            else
            {
                await SendEmailInviteAsync(user.Id, inviteModel.InvitationMessage);
            }

            return result;
        }

        public async Task<InviteModel> GetInviteByIdAsync(string id)
        {
            return await _inviteRepository.GetInviteByIdAsync(id);
        }

        public async Task<IEnumerable<InviteModel>> GetSentInvitesAsync(string userId)
        {
            return await _inviteRepository.GetSentInvitesAsync(userId);
        }
        public async Task<IEnumerable<InviteModel>> GetReceivedInvitesAsync(string userId)
        {
            return await _inviteRepository.GetReceivedInvitesAsync(userId);
        }

        public async Task UpdateInviteAsync(InviteModel invite)
        {
            await _inviteRepository.UpdateInviteAsync(invite);
        }
        public async Task UpdateInviteStatusAsync(string inviteId, int status)
        {
            await _inviteRepository.UpdateInviteStatusAsync(inviteId, status);
        }

        private async Task SendNotificationAsync(string userId, string message)
        {
            // Logic to send notification
        }

        private async Task SendEmailInviteAsync(string email, string message)
        {
            // Logic to send email invite
        }
    }
}
