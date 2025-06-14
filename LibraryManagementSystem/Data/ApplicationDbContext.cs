using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    public DbSet<Book> Books { get; set; }
    public DbSet<BorrowingHistory> BorrowingHistories { get; set; }
    public DbSet<ApplicationUser> ApplicationUsers { get; set; }
    public DbSet<BorrowRequest> BorrowRequests { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<BookSubject> BookSubjects { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<BookCopy> BookCopies { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BookSubject>()
            .HasKey(bs => new { bs.BookId, bs.SubjectId });

        modelBuilder.Entity<BookSubject>()
            .HasOne(bs => bs.Book)
            .WithMany(b => b.BookSubjects)
            .HasForeignKey(bs => bs.BookId);

        modelBuilder.Entity<BookSubject>()
            .HasOne(bs => bs.Subject)
            .WithMany(s => s.BookSubjects)
            .HasForeignKey(bs => bs.SubjectId);

        // Configure relationships
        modelBuilder.Entity<BorrowingHistory>()
            .HasOne(bh => bh.Book)
            .WithMany(b => b.BorrowingHistories)
            .HasForeignKey(bh => bh.BookID);

        modelBuilder.Entity<BorrowingHistory>()
            .HasOne(bh => bh.User)
            .WithMany(u => u.BorrowingHistories)
            .HasForeignKey(bh => bh.UserID);

        // Configure BookCopy relationships
        modelBuilder.Entity<BookCopy>()
            .HasOne(bc => bc.Book)
            .WithMany(b => b.BookCopies)
            .HasForeignKey(bc => bc.BookID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ActivityLog>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserID)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActivityLog>()
            .HasOne(a => a.AffectedUser)
            .WithMany()
            .HasForeignKey(a => a.AffectedUserID)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActivityLog>()
            .HasOne(a => a.Book)
            .WithMany()
            .HasForeignKey(a => a.BookID)
            .OnDelete(DeleteBehavior.Restrict);
    }
}