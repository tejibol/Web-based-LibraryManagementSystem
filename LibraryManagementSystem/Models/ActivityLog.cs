using System;

namespace LibraryManagementSystem.Models
{
    public class ActivityLog
    {
        public int ActivityLogID { get; set; }
        public string ActivityType { get; set; }  // BorrowRequest, RequestAction, BookReturn, Overdue, BookAdded
        public string Description { get; set; }
        public string UserID { get; set; }        // User who performed the action
        public string AffectedUserID { get; set; } // User affected by the action (e.g., student who borrowed)
        public int? BookID { get; set; }          // Related book (if any)
        public DateTime Timestamp { get; set; }
        public string AdditionalInfo { get; set; } // JSON string for any additional data

        // Navigation properties
        public ApplicationUser User { get; set; }
        public ApplicationUser AffectedUser { get; set; }
        public Book Book { get; set; }
    }

    public static class ActivityType
    {
        public const string BorrowRequest = "BorrowRequest";
        public const string RequestApproved = "RequestApproved";
        public const string RequestRejected = "RequestRejected";
        public const string BookReturned = "BookReturned";
        public const string BookOverdue = "BookOverdue";
        public const string BookAdded = "BookAdded";
        public const string BookUpdated = "BookUpdated";
        public const string BookAvailabilityChanged = "BookAvailabilityChanged";
        public const string UserArchived = "UserArchived";
        public const string UserUnarchived = "UserUnarchived";
        public const string UserEdited = "UserEdited";
        public const string UserCreated = "UserCreated";
        public const string SystemMaintenance = "SystemMaintenance";
    }
} 