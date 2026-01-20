namespace AuthServer.Api.Models
{
    public class PermissionModel : DocumentBase
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int GlobalRole { get; set; } = 1; // 1 = App, 2 = App
        public string? AppId { get; set; } = null;

    }
}
