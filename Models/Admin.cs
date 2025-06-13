using System.ComponentModel.DataAnnotations;

namespace SocSurvey2.Models
{
    public class Admin
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; }

        [Required]
        public string PasswordHash { get; set; }
    }
}