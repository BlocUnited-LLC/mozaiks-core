namespace AuthServer.Api.Models
{
    public class RoleModel : DocumentBase
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Permissions { get; set; } = new List<string>();
        public string AppId { get; set; } = string.Empty;
    }
}
