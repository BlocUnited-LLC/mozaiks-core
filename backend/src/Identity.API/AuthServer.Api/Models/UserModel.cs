using AuthServer.Api.Shared;
using MongoDB.Bson.Serialization.Attributes;

namespace AuthServer.Api.Models
{
    /// <summary>
    /// CONTROL PLANE ENTITY: Platform User
    /// 
    /// This is the authoritative record for a Mozaiks platform user.
    /// Collection: AppUsers (MongoDB)
    /// Source of Truth: This service + External IdP (CIAM) for credentials
    /// 
    /// Related entities:
    /// - MozaiksApps: Apps owned by this user (via OwnerUserId)
    /// - Teams: Team memberships (via UserId)
    /// - Transactions: Subscription/payment records (via Metadata.UserId)
    /// - Investments: Funding round investments (via InvestorUserId)
    /// 
    /// Authentication: External IdP issues JWT, this service validates and maintains profile.
    /// Keycloak is NOT used for platform user authentication.
    /// </summary>
    public class UserModel : DocumentBase
    {
        public required string FirstName { get; set; }
        public required string LastName { get; set; }

        public required string Email { get; set; }  
        public string? Phone { get; set; }
        public Nullable<DateTime> DOB { get; set; }
        public string? UserPhoto { get; set; }
        public string? Bio { get; set; }
        public bool PhoneVerified { get; set; } = false;
        public bool EmailVerified { get; set; } = false;
        public string? LockoutEnd { get; set; }= "";
        public bool TwoFactorEnabled { get; set; } = false;
        public bool LockoutEnabled { get; set; } = false;
        public int AccessFailedCount { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedByUserId { get; set; }
        //public FreelancerProfileModel? FreelancerProfile { get; set; }
        public UserRole[] UserRoles { get; set; } = new[] { UserRole.User };
        public int OnboardingStatus { get; set; } = 0;

        public void AddRole(UserRole newRole)
        {
            UserRole[] updatedRoles = new UserRole[UserRoles.Length + 1];

            Array.Copy(UserRoles, updatedRoles, UserRoles.Length);

            updatedRoles[UserRoles.Length] = newRole;

            UserRoles = updatedRoles;
        }

        public List<UserRoleModel> Roles { get; set; } = new List<UserRoleModel>();
        public SubscriptionPlanModel? SubscriptionPlan { get; set; } = new();
    }
}
