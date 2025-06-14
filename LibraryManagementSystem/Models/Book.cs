using System;
using System.ComponentModel.DataAnnotations;
namespace LibraryManagementSystem.Models;
using LibraryManagementSystem.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Book
{
    public Book()
    {
        // Ensure collections are initialized
        BookSubjects = new List<BookSubject>();
        BorrowingHistories = new List<BorrowingHistory>();
        BookCopies = new List<BookCopy>();
    }

    [Key]
    public int BookID { get; set; }

    [NotMapped]
    public string BookNumber => BookID.ToString("D4");

    [Required]
    [StringLength(100)]
    public string Title { get; set; }

    [Required]
    [StringLength(50)]
    public string Author { get; set; }

    [StringLength(20)]
    public string? ISBN { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public DateTime PublishedDate { get; set; }

    public string? CoverImageUrl { get; set; }

    [Required]
    public bool IsAvailable { get; set; } = true;

    public bool IsArchived { get; set; } = false;

    public virtual ICollection<BorrowingHistory> BorrowingHistories { get; set; }

    public ICollection<BookSubject> BookSubjects { get; set; }

    public ICollection<BookCopy> BookCopies { get; set; }
}