namespace LibraryManagementSystem.Models
{
    public class UserViewModel
    {
        public string UserId { get; set; } // Use string for IdentityUser's Id
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public DateTime CreatedAt { get; set; } // Adjust if needed
    }



}
