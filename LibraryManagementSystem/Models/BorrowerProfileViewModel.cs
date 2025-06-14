using System;
using System.Collections.Generic;

namespace LibraryManagementSystem.Models
{
    public class BorrowerProfileViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public List<BorrowingHistory> CurrentBorrowings { get; set; } = new();
        public List<BorrowingHistory> BorrowingHistory { get; set; } = new();
    }
} 