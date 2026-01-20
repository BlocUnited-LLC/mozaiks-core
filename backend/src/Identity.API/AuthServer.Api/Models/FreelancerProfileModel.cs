namespace AuthServer.Api.Models
{
    public class FreelancerProfileModel : DocumentBase
    {
        public string ProfessionalTitle { get; set; }
        public string HigherEducation { get; set; }
        public string[] LanguageSpoken { get; set; }
        public string Availablity { get; set; }
        public decimal HourlyRate { get; set; }
        public SkillModel[] Skills { get; set; }
        public string HowHearAboutUs { get; set; }
        public string FreelancerBio { get; set; } 
        public string GithubId { get; set; } = string.Empty;
        public string LinkedInId { get; set; } = string.Empty;
        public string FreelancerPhoto { get; set; } = string.Empty;


    }
}
