using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibraryManagementSystem.Models
{
    public class BorrowRequest
    {
        [Key]
        public int RequestID { get; set; }

        [Required]
        public string Status { get; set; } = "Pending";

        [Required]
        [ForeignKey("Book")]
        public int BookID { get; set; }

        [Required]
        [ForeignKey("User")]
        public string UserID { get; set; }

        public DateTime RequestDate { get; set; } = DateTime.Now; // Default value para auto kapag gumawa ng request

        public bool IsFulfilled { get; set; } = false; // Default pending kapag bagong request

        // Navigation properties
        public virtual Book Book { get; set; }
        public virtual ApplicationUser User { get; set; }
    }
}
