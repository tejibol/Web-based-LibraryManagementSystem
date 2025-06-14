namespace LibraryManagementSystem.Models
{
    public class Notification
    {
        public int NotificationId { get; set; }

        public string Message { get; set; }

        public string? Link { get; set; } // Optional, like redirect to Pending Requests

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        // For Borrower-specific notifications
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        // For Role-based notifications (Admins/Librarians)
        public string? TargetRole { get; set; }
    }
}
