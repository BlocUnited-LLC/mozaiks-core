using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AuthServer.Api.Services
{
    public class UserService
    {
        private readonly IUserRepository _userRepository;
        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }
        public async Task<UserInfoResponse> GetUserByEmailAsync(string email)
        {
            var user = await _userRepository.GetUserByEmailAsync(email);
            if(user != null)
            {
                return UserMapping(user);
            }
            return null;
        }

        public async Task<bool> CreateUserAsync(UserModel user)
        {
            return await _userRepository.CreateUserAsync(user);
        }

        public async Task<UserInfoResponse> GetUserByIdAsync(string id)
        {
            var user= await _userRepository.GetUserByIdAsync(id);
            return UserMapping(user);
        }
        

        public async Task<IEnumerable<UserModel>> GetUsersByRoleAsync(int role)
        {
            return await _userRepository.GetUsersByRoleAsync(role);
        }

        public async Task<IEnumerable<UserModel>> GetAllUsersAsync()
        {
            return await _userRepository.GetAllUsersAsync();
        }
        public async Task<IEnumerable<UserModel>> GetUsersByIdsAsync(IEnumerable<string> userIds)
        {
            return await _userRepository.GetUsersByIdsAsync(userIds);
        }

        public async Task RevokeUserAsync(string id)
        {
            await _userRepository.RevokeUserAsync(id);
        }

        public async Task SoftDeleteUserAsync(string id, string deletedByUserId)
        {
            await _userRepository.SoftDeleteUserAsync(id, deletedByUserId);
        }

        public async Task UpdateUserAsync(UserModel user)
        {
            await _userRepository.UpdateUserAsync(user);
        }
        public async Task UpdateUserProfileAsync(string userId, UserModel user)
        {
            await _userRepository.UpdateUserProfileAsync(userId, user);
        }
        private UserInfoResponse UserMapping(UserModel user)
        {
            UserInfoResponse userInfo = new UserInfoResponse()
            { 
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                //AccessFailedCount = user.AccessFailedCount,
                //DOB = user.DOB,
                EmailVerified = user.EmailVerified,
                Id = user.Id,
                IsActive = user.IsActive,
                Phone = user.Phone,
                UserPhoto = user.UserPhoto,
                //LockoutEnabled = user.LockoutEnabled,
                //LockoutEnd = user.LockoutEnd,
                //PhoneVerified = user.PhoneVerified,
                //TwoFactorEnabled = user.TwoFactorEnabled,
                UserRoles = user.UserRoles,
                Bio=user.Bio,
                

            };
            return userInfo;
        }
    }
}
