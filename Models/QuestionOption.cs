using SocSurvey2.Models;

public class QuestionOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string OptionText { get; set; }
    public int Order { get; set; }

    public Question Question { get; set; }
}
