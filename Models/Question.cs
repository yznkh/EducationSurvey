namespace SocSurvey2.Models
{
    public class Question
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public string Category { get; set; } // e.g., IT Asset Inventory, SOC Capabilities
        public string Sector { get; set; } // education, health, or null if shared

                public string Type { get; set; } = "text"; // New
        public bool IsRequired { get; set; } = false; // New

        public ICollection<QuestionOption> Options { get; set; } // Optional if you add options table
    }
}
