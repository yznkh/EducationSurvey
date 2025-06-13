namespace SocSurvey2.Models
{
    public class SurveySubmission
    {
        public int Id { get; set; }
        public string OrganizationName { get; set; }
        public DateTime SubmissionDate { get; set; }
        public string Sector { get; set; } // education or health

        public List<SurveyAnswer> Answers { get; set; }


    }
}
