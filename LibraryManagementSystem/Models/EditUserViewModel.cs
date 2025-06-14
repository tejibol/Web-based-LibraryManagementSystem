using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    public class EditUserViewModel
    {
        [Required]
        public string Id { get; set; }
        
        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        // Password is optional for editing
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [Required]
        public string Role { get; set; } // Role string to assign & display

        // Student-only (optional)
        public string? LRN { get; set; }
        public string? Section { get; set; }

        [RegularExpression(@"^Grade (7|8|9|10)$", ErrorMessage = "Year level must be Grade 7-10")]
        public string? YearLevel { get; set; }
    }
} 