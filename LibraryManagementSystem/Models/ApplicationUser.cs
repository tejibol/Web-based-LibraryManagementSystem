using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    public class ApplicationUser : IdentityUser
    {
        // Additional properties for the user
        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } // User's first name

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } // User's last name

        // Optional fields for students
        public string? LRN { get; set; }
        public string? Section { get; set; }
        public string? YearLevel { get; set; }

        public string Role { get; set; } // Role of the user (e.g., Student, Librarian, Admin)

        // Optional: Add more properties as needed
        public DateTime DateOfBirth { get; set; } // User's date of birth
        public string ProfilePictureUrl { get; set; }
        public bool IsArchived { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<BorrowingHistory> BorrowingHistories { get; set; }
    }
}
