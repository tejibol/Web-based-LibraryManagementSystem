using System;

namespace LibraryManagementSystem.Models
{
    public class ActivityLogViewModel
    {
        public int ActivityLogID { get; set; }
        public string ActivityType { get; set; }
        public string Description { get; set; }
        public string PerformedBy { get; set; }  // Name of user who performed the action
        public string AffectedUser { get; set; } // Name of user affected by the action
        public string BookTitle { get; set; }    // Title of the related book
        public DateTime Timestamp { get; set; }
        public string AdditionalInfo { get; set; }

        // Helper properties for UI
        public string ActivityIcon
        {
            get => ActivityType switch
            {
                "BorrowRequest" => "fa-book",
                "RequestApproved" => "fa-check-circle",
                "RequestRejected" => "fa-times-circle",
                "BookReturned" => "fa-undo",
                "BookOverdue" => "fa-exclamation-circle",
                "BookAdded" => "fa-plus-circle",
                "BookUpdated" => "fa-edit",
                "UserArchived" => "fa-archive",
                "UserUnarchived" => "fa-box-open",
                "UserCreated" => "fa-user-plus",
                "SystemMaintenance" => "fa-cogs",
                _ => "fa-info-circle"
            };
        }

        public string ActivityColor
        {
            get => ActivityType switch
            {
                "BorrowRequest" => "primary",
                "RequestApproved" => "success",
                "RequestRejected" => "danger",
                "BookReturned" => "info",
                "BookOverdue" => "warning",
                "BookAdded" => "success",
                "BookUpdated" => "primary",
                "UserArchived" => "secondary",
                "UserUnarchived" => "info",
                "UserCreated" => "success",
                "SystemMaintenance" => "dark",
                _ => "secondary"
            };
        }
    }
} 