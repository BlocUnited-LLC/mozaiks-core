namespace AuthServer.Api.Models
{
    public class UserRoleModel: DocumentBase
    {
        public string RoleId { get; set; }
        public string AppId { get; set; }
        public string RoleLevel { get; set; } // Internal or Preferred
    }
}
