using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibraryManagementSystem.Models
{
    public class BookCopy
    {
        [Key]
        public int CopyID { get; set; }

        [Required]
        public string CopyNumber { get; set; } // Format: padded BookID + CN + copy number (e.g., 0001CN1)

        [Required]
        [ForeignKey("Book")]
        public int BookID { get; set; }

        [Required]
        public bool IsAvailable { get; set; } = true;

        public string? Condition { get; set; } // e.g., "New", "Good", "Fair", "Poor"

        public string? Notes { get; set; } // Any additional notes about this specific copy

        // Navigation property
        public virtual Book Book { get; set; }

        // Navigation property for borrowing history
        public virtual ICollection<BorrowingHistory> BorrowingHistories { get; set; }
    }
}