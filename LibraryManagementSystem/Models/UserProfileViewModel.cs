using System;
using System.Collections.Generic;

namespace LibraryManagementSystem.Models
{
    public class UserProfileViewModel
    {
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string? LRN { get; set; }
        public string? Section { get; set; }
        public string? YearLevel { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        // New properties for borrowings
        public List<BorrowingHistory> CurrentBorrowings { get; set; } = new();
        public List<BorrowingHistory> BorrowingHistory { get; set; } = new();
        
        // New property for most borrowed books
        public List<MostBorrowedBook> MostBorrowedBooks { get; set; } = new();
    }

    public class MostBorrowedBook
    {
        public int BookId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string CoverImageUrl { get; set; }
        public int BorrowCount { get; set; }
        public DateTime LastBorrowed { get; set; }
    }
}