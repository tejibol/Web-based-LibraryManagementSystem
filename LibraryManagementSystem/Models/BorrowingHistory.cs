using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibraryManagementSystem.Models
{
    public class BorrowingHistory
    {
        [Key]
        public int BorrowingHistoryID { get; set; }

        // Foreign key to Book (int type)
        [Required]
        public int BookID { get; set; }

        // Foreign key to User (string type)
        [Required]
        public string UserID { get; set; }

        [Required]
        public DateTime BorrowDate { get; set; }

        public string CopyNumber { get; set; }
        public int CopyID { get; set; }
        public DateTime? ReturnDate { get; set; }

        [Required]
        public bool IsReturned { get; set; }

        public DateTime DueDate { get; set; }

        // Navigation properties
        [ForeignKey("BookID")]
        public virtual Book Book { get; set; }

        [ForeignKey("UserID")]
        public virtual ApplicationUser User { get; set; }
    }
}
