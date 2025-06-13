using Microsoft.EntityFrameworkCore;
using SocSurvey2.Models;

namespace SocSurvey2.Data
{
    public class SurveyContext : DbContext
    {
        public SurveyContext(DbContextOptions<SurveyContext> options) : base(options)
        {
        }

        public DbSet<SurveySubmission> SurveySubmissions { get; set; }
        public DbSet<SurveyAnswer> SurveyAnswers { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<QuestionOption> QuestionOptions { get; set; } // إذا أنشأته


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SurveySubmission>()
                .HasMany(s => s.Answers)
                .WithOne(a => a.SurveySubmission)
                .HasForeignKey(a => a.SurveySubmissionId);

           

            modelBuilder.Entity<Admin>().HasData(
    new Admin
    {
        Id = 1,
        Username = "admin1",
        PasswordHash = "$2a$11$5s2v8i2Qz6z9t3y7u8w9xO2v3x4y5z6A7B8C9D0E1F2G3H4I5J6K" // تجزئة Admin@123
    },
    new Admin
    {
        Id = 2,
        Username = "admin2",
        PasswordHash = "$2a$11$j9k8l7m6n5o4p3q2r1s0t9u8v7w6x5y4z3A2B1C0D9E8F7G6H5I4J" // تجزئة Password123@
    }
);
        }
    }
}