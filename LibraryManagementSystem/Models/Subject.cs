using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.Models
{
    public class Subject
    {
        [Key]
        public int SubjectId { get; set; }

        [Required]
        [StringLength(100)]
        public string SubjectName { get; set; } // e.g., "Computer Science", "Literature"

        // Many-to-Many Relationship with Books
        public ICollection<BookSubject> BookSubjects { get; set; }
    }

    public class BookSubject
    {
        public int BookId { get; set; }
        public Book Book { get; set; }

        public int SubjectId { get; set; }
        public Subject Subject { get; set; }
    }
}