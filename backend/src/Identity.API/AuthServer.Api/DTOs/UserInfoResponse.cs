using AuthServer.Api.Models;
using AuthServer.Api.Shared;

namespace AuthServer.Api.DTOs
{
    public class UserInfoResponse
    {
        public string Id { get; set; }
        public string FirstName { get; set; }

        public string LastName { get; set; }

        public required string Email { get; set; }

        public string? Phone { get; set; }
        public string? Bio { get; set; }

        //public DateTime? DOB { get; set; }

        public string? UserPhoto { get; set; }

        public bool PhoneVerified { get; set; } = false;

        public bool EmailVerified { get; set; } = false;

        public DateTimeOffset? LockoutEnd { get; set; }

        public bool TwoFactorEnabled { get; set; } = false;

        public bool LockoutEnabled { get; set; } = false;

        public int AccessFailedCount { get; set; } = 0;

        public bool IsActive { get; set; } = true;
         
        public UserRole[] UserRoles { get; set; }
        public int OnboardingStatus { get; set; } = 0;
    }
}
