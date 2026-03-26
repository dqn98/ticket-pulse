using System;
using System.ComponentModel.DataAnnotations;

namespace IdentityService.Data
{
    public class User
    {
        public int Id { get; set; }
        
        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string Role { get; set; } = "User";
    }
}
