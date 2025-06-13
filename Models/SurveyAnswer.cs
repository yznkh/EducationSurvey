namespace SocSurvey2.Models
{
    public class SurveyAnswer
    {
        public int Id { get; set; }
        public int SurveySubmissionId { get; set; }
        public int QuestionId { get; set; }
        public string Value { get; set; }
        public SurveySubmission SurveySubmission { get; set; } // إضافة خاصية الملاحة
    }
}