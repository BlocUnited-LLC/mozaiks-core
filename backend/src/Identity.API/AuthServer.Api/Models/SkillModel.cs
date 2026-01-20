using System.ComponentModel.DataAnnotations;

namespace AuthServer.Api.Models
{
    public class SkillModel
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public int ExperienceInMonths { get; set; }
        [Required]
        public int LastUsedYear { get; set; }
    }
}
