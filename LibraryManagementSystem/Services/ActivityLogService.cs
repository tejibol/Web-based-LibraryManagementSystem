using System;
using System.Threading.Tasks;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace LibraryManagementSystem.Services
{
    public class ActivityLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ActivityLogService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task LogActivity(string activityType, string description, string userId, string affectedUserId = null, int? bookId = null, string additionalInfo = null)
        {
            var log = new ActivityLog
            {
                ActivityType = activityType,
                Description = description,
                UserID = userId,
                AffectedUserID = affectedUserId ?? userId,
                BookID = bookId,
                Timestamp = DateTime.Now,
                AdditionalInfo = additionalInfo ?? "{}"
            };

            _context.ActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task LogBorrowRequest(string studentId, int bookId, int requestId)
        {
            var book = await _context.Books.FindAsync(bookId);
            var student = await _context.Users.FindAsync(studentId);
            
            await LogActivity(
                ActivityType.BorrowRequest,
                $"Borrow request for '{book.Title}'",
                studentId,
                studentId,
                bookId,
                $"{{\"RequestID\": {requestId}}}"
            );
        }

        public async Task LogRequestAction(string librarianId, string studentId, int bookId, bool isApproved, int requestId)
        {
            var book = await _context.Books.FindAsync(bookId);
            var actionType = isApproved ? ActivityType.RequestApproved : ActivityType.RequestRejected;
            
            await LogActivity(
                actionType,
                $"{(isApproved ? "Approved" : "Rejected")} borrow request for '{book.Title}'",
                librarianId,
                studentId,
                bookId,
                $"{{\"RequestID\": {requestId}}}"
            );
        }

        public async Task LogBookReturn(string librarianId, string studentId, int bookId, int borrowingId)
        {
            var book = await _context.Books.FindAsync(bookId);
            
            await LogActivity(
                ActivityType.BookReturned,
                $"Book '{book.Title}' returned",
                librarianId,
                studentId,
                bookId,
                $"{{\"BorrowingID\": {borrowingId}}}"
            );
        }

        public async Task LogBookOverdue(string studentId, int bookId, int borrowingId)
        {
            var book = await _context.Books.FindAsync(bookId);
            
            await LogActivity(
                ActivityType.BookOverdue,
                $"Book '{book.Title}' is overdue",
                studentId,
                studentId,
                bookId,
                $"{{\"BorrowingID\": {borrowingId}}}"
            );
        }

        public async Task LogBookAdded(string librarianId, int bookId)
        {
            var book = await _context.Books.FindAsync(bookId);
            
            await LogActivity(
                ActivityType.BookAdded,
                $"New book added: '{book.Title}'",
                librarianId,
                null,
                bookId
            );
        }

        public async Task LogBookUpdated(string librarianId, int bookId)
        {
            var book = await _context.Books
                .Include(b => b.BookCopies)
                .FirstOrDefaultAsync(b => b.BookID == bookId);
            
            if (book != null)
            {
                var totalCopies = book.BookCopies.Count;
                var availableCopies = book.BookCopies.Count(c => c.IsAvailable);
                
                await LogActivity(
                    ActivityType.BookUpdated,
                    $"Updated book: '{book.Title}' (Total Copies: {totalCopies}, Available: {availableCopies})",
                    librarianId,
                    null,
                    bookId,
                    $"{{\"TotalCopies\": {totalCopies}, \"AvailableCopies\": {availableCopies}}}"
                );
            }
        }

        public async Task LogBookAvailabilityChanged(string userId, int bookId, bool isAvailable)
        {
            var book = await _context.Books.FindAsync(bookId);
            var statusText = isAvailable ? "Available" : "Not Available";
            
            await LogActivity(
                ActivityType.BookAvailabilityChanged,
                $"Changed book availability status: '{book.Title}' is now {statusText}",
                userId,
                null,
                bookId,
                $"{{\"AvailabilityChanged\": true, \"IsAvailable\": {isAvailable.ToString().ToLower()}}}"
            );
        }
    }
} 