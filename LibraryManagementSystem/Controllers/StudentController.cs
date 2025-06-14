using LibraryManagementSystem.Data;
using LibraryManagementSystem.Hubs;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    public class StudentController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ActivityLogService _activityLogService;

        public StudentController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IHubContext<NotificationHub> hubContext,
            ActivityLogService activityLogService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _hubContext = hubContext;
            _activityLogService = activityLogService;
        }
        public IActionResult Index()
        {
            return View();
        }
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);

            // Newly Added Books
            var newBooks = await _context.Books
                .Where(b => !b.IsArchived)
                .OrderByDescending(b => b.BookID)
                .Take(3)
                .Include(b => b.BookSubjects)
                .ThenInclude(bs => bs.Subject)
                .ToListAsync();

            // Current Borrowings
            var currentBorrowings = await _context.BorrowingHistories
                .Include(b => b.Book)
                .Where(b => b.UserID == user.Id && b.ReturnDate == null)
                .OrderByDescending(b => b.BorrowDate)
                .ToListAsync();

            // Borrowing History
            var borrowingHistory = await _context.BorrowingHistories
                .Include(b => b.Book)
                .Where(b => b.UserID == user.Id && b.ReturnDate != null)
                .OrderByDescending(b => b.BorrowDate)
                .ToListAsync();

            ViewBag.NewBooks = newBooks;
            ViewBag.CurrentBorrowings = currentBorrowings;
            ViewBag.BorrowingHistory = borrowingHistory;
            ViewBag.CurrentUser = user;

            return View();
        }
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");  // Redirect to Home page or Login page
        }
        public async Task<IActionResult> Collections()
        {
            var user = await _userManager.GetUserAsync(User);

            var books = await _context.Books
                .Where(b => !b.IsArchived)
                .Include(b => b.BookSubjects)
                .ThenInclude(bs => bs.Subject)
                .ToListAsync();

            // Get current user's borrow status
            var currentBorrows = await _context.BorrowingHistories
                .Where(b => b.UserID == user.Id && !b.IsReturned)
                .ToListAsync();

            var pendingRequests = await _context.BorrowRequests
                .Where(r => r.UserID == user.Id && r.Status == "Pending")
                .ToListAsync();

            // Get all books that user has borrowed or requested
            var borrowedBookIds = currentBorrows.Select(b => b.BookID).ToList();
            var requestedBookIds = pendingRequests.Select(r => r.BookID).ToList();

            ViewBag.CurrentBorrows = currentBorrows;
            ViewBag.PendingRequests = pendingRequests;
            ViewBag.CurrentBorrowCount = currentBorrows.Count;
            ViewBag.PendingRequestCount = pendingRequests.Count;
            ViewBag.BorrowedBookIds = borrowedBookIds;
            ViewBag.RequestedBookIds = requestedBookIds;
            ViewBag.CanRequestMore = currentBorrows.Count + pendingRequests.Count < 2;

            return View(books);
        }
        public async Task<IActionResult> ViewBook(int id)
        {
            var book = await _context.Books
                .Include(b => b.BookSubjects)
                .ThenInclude(bs => bs.Subject)
                .FirstOrDefaultAsync(b => b.BookID == id);

            if (book == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Get current borrowings count
            var currentBorrows = await _context.BorrowingHistories
                .CountAsync(b => b.UserID == user.Id && !b.IsReturned);

            // Get pending requests count
            var pendingRequests = await _context.BorrowRequests
                .CountAsync(r => r.UserID == user.Id && r.Status == "Pending");

            // Check if user has already borrowed this book
            var hasBorrowedCopy = await _context.BorrowingHistories
                .AnyAsync(b => b.UserID == user.Id &&
                             b.BookID == id &&
                             !b.IsReturned);

            // Check if user has a pending request for this book
            var hasPendingRequest = await _context.BorrowRequests
                .AnyAsync(r => r.UserID == user.Id &&
                             r.BookID == id &&
                             r.Status == "Pending");

            ViewBag.CurrentBorrows = currentBorrows;
            ViewBag.PendingRequests = pendingRequests;
            ViewBag.HasBorrowedCopy = hasBorrowedCopy;
            ViewBag.HasPendingRequest = hasPendingRequest;
            ViewBag.CanRequest = currentBorrows + pendingRequests < 2 && 
                                !hasBorrowedCopy && 
                                !hasPendingRequest && 
                                book.IsAvailable && 
                                !book.IsArchived;

            return View(book);
        }

        // GET: Students/RequestBorrow/5

        [Authorize(Roles = "Student,Teacher")]
        public async Task<IActionResult> RequestBook(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Check if book is archived
            if (book.IsArchived)
            {
                TempData["Error"] = "This book is currently archived and cannot be requested.";
                return RedirectToAction("ViewBook", new { id });
            }

            // 🔽 Additional checks
            var currentBorrowCount = await _context.BorrowingHistories
                .CountAsync(b => b.UserID == user.Id && b.ReturnDate == null);

            var pendingRequestCount = await _context.BorrowRequests
                .CountAsync(r => r.UserID == user.Id && r.Status == "Pending");
                
            // In StudentController.RequestBook action
            var hasBorrowedCopy = await _context.BorrowingHistories
                .AnyAsync(b => b.UserID == user.Id &&
                             b.BookID == id &&
                             b.ReturnDate == null);

            if (hasBorrowedCopy)
            {
                TempData["Error"] = "You already have this book borrowed";
                return RedirectToAction("ViewBook", new { id });
            }

            if (currentBorrowCount >= 2)
            {
                TempData["Error"] = "Maximum of 2 books allowed. Return books to request new ones.";
                return RedirectToAction("Collections");
            }

            if (pendingRequestCount >= 2)
            {
                TempData["Error"] = "Maximum of 2 pending requests allowed.";
                return RedirectToAction("Collections");
            }

            if (!book.IsAvailable)
            {
                TempData["Error"] = "This book is currently unavailable.";
                return RedirectToAction("Collections");
            }

            // ✅ Original check: existing request
            var existingRequest = await _context.BorrowRequests
                .AnyAsync(r => r.UserID == user.Id && r.BookID == id && r.Status == "Pending");

            if (existingRequest)
            {
                TempData["Error"] = "You already have a pending request for this book.";
                return RedirectToAction("ViewBook", new { id });
            }

            // ✅ Original check: current borrows
            var currentBorrows = await _context.BorrowingHistories
                .CountAsync(b => b.UserID == user.Id && b.ReturnDate == null);

            if (currentBorrows >= 2)
            {
                TempData["Error"] = "Maximum of 2 books allowed. Return books to request new ones.";
                return RedirectToAction("ViewBook", new { id });
            }

            return View(book);
        }

        [HttpPost]
        [Authorize(Roles = "Student,Teacher")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRequest(int bookId)
        {
            try
            {
                var book = await _context.Books.FindAsync(bookId);
                var user = await _userManager.GetUserAsync(User);

                if (book == null || user == null)
                {
                    return Json(new { success = false, message = "Invalid request" });
                }

                // Create request
                var request = new BorrowRequest
                {
                    BookID = bookId,
                    UserID = user.Id,
                    Status = "Pending",
                    RequestDate = DateTime.Now
                };
                _context.BorrowRequests.Add(request);

                // Create notification for librarians
                var notification = new Notification
                {
                    TargetRole = "Librarian",
                    Message = $"New Request: {book.Title} by {user.FirstName} {user.LastName}",
                    Link = "/Librarian/PendingRequest",
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();

                // Log the activity
                await _activityLogService.LogBorrowRequest(
                    user.Id,
                    bookId,
                    request.RequestID
                );

                return Json(new
                {
                    success = true,
                    redirectUrl = Url.Action("ViewBook", "Student", new { id = bookId })
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "An error occurred while processing your request"
                });
            }
        }
    }
}