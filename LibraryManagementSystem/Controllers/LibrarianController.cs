using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Hubs;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.Controllers
{
    public class LibrarianController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ActivityLogService _activityLogService;

        public LibrarianController(
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

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.PendingRequests = await _context.BorrowRequests
                .Where(r => r.Status == "Pending" && !r.IsFulfilled)
                .CountAsync();

            ViewBag.OverdueBooks = _context.BorrowingHistories
                .Where(b => b.ReturnDate == null && b.DueDate < DateTime.Now)
                .Count();

            ViewBag.ActiveBorrowings = await _context.BorrowingHistories
                .Where(b => b.ReturnDate == null)
                .CountAsync();

            // Add count of total book copies in the library (excluding archived books)
            ViewBag.TotalBookCopies = await _context.BookCopies
                .Include(c => c.Book)
                .Where(c => !c.Book.IsArchived)
                .CountAsync();

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AddBook()
        {
            ViewBag.AllSubjects = await _context.Subjects.ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBook(Book book, IFormFile coverImage, List<int> selectedSubjectIds, int totalCopies = 1)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (coverImage != null && coverImage.Length > 0)
                    {
                        var uploadsFolder = Path.Combine("wwwroot", "images", "books");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(coverImage.FileName);
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await coverImage.CopyToAsync(fileStream);
                        }

                        book.CoverImageUrl = "/images/books/" + uniqueFileName;
                    }
                    else
                    {
                        book.CoverImageUrl = "/images/books/default.jpg";
                    }

                    if (selectedSubjectIds != null && selectedSubjectIds.Any())
                    {
                        foreach (var subjectId in selectedSubjectIds)
                        {
                            book.BookSubjects.Add(new BookSubject { SubjectId = subjectId, BookId = book.BookID });
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("selectedSubjectIds", "Please select at least one subject");
                        ViewBag.AllSubjects = await _context.Subjects.ToListAsync();
                        return View(book);
                    }

                    // Add the book first to get the BookID
                    _context.Books.Add(book);
                    await _context.SaveChangesAsync();

                    // Now add the copies using the generated BookID
                    for (int i = 1; i <= totalCopies; i++)
                    {
                        var copyNumber = $"{book.BookNumber}CN{i}";
                        _context.BookCopies.Add(new BookCopy
                        {
                            BookID = book.BookID,
                            CopyNumber = copyNumber,
                            IsAvailable = true
                        });
                    }

                    await _context.SaveChangesAsync();

                    // Log the activity
                    var currentUser = await _userManager.GetUserAsync(User);
                    await _activityLogService.LogBookAdded(currentUser.Id, book.BookID);

                    TempData["Success"] = "Book added successfully!";
                    return RedirectToAction(nameof(ManageBooks));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error: {ex.Message}");
                    TempData["Error"] = "Failed to add book. Please try again.";
                    Console.WriteLine($"ERROR: {ex}");
                }
            }

            ViewBag.AllSubjects = await _context.Subjects.ToListAsync();
            return View(book);
        }

        [HttpGet]
        public async Task<IActionResult> ManageBooks()
        {
            var books = await _context.Books
                .Where(b => !b.IsArchived) // Only show non-archived books
                .Include(b => b.BookSubjects)
                .ThenInclude(bs => bs.Subject)
                .OrderBy(b => b.Title)
                .ToListAsync();

            return View(books);
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult PendingRequest()
        {
            var requests = _context.BorrowRequests
                .Include(r => r.Book)
                .Include(r => r.User)
                .Where(r => r.Status == "Pending")
                .Select(r => new BorrowRequestViewModel
                {
                    RequestID = r.RequestID,
                    BookTitle = r.Book.Title,
                    BorrowerName = r.User.LastName + " " + r.User.FirstName,
                    UserId = r.UserID,
                    RequestDate = r.RequestDate,
                    Status = r.Status
                }).ToList();

            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRequestStatus(int id, string status)
        {
            try
            {
                // Load the request with all necessary related data
                var request = await _context.BorrowRequests
                    .Include(r => r.User)
                    .Include(r => r.Book)
                    .ThenInclude(b => b.BookCopies)
                    .FirstOrDefaultAsync(r => r.RequestID == id);

                if (request == null)
                {
                    TempData["Error"] = "Request not found.";
                    return RedirectToAction("PendingRequest");
                }

                request.Status = status;
                request.IsFulfilled = true;

                if (status == "Approved")
                {
                    // Find the first available copy
                    var availableCopy = request.Book.BookCopies
                        .Where(c => c.IsAvailable)
                        .OrderBy(c => c.CopyNumber)
                        .FirstOrDefault();

                    if (availableCopy == null)
                    {
                        TempData["Error"] = "No available copies of this book.";
                        return RedirectToAction("PendingRequest");
                    }

                    // Mark the copy as unavailable
                    availableCopy.IsAvailable = false;

                    // Create borrowing history record
                    var borrowingHistory = new BorrowingHistory
                    {
                        UserID = request.UserID,
                        BookID = request.BookID,
                        CopyID = availableCopy.CopyID,
                        CopyNumber = availableCopy.CopyNumber, // Set the CopyNumber from the available copy
                        BorrowDate = DateTime.Now,
                        DueDate = DateTime.Now.AddDays(7),
                        ReturnDate = null,
                        IsReturned = false
                    };

                    _context.BorrowingHistories.Add(borrowingHistory);
                }

                // Create student notification
                var studentNotification = new Notification
                {
                    UserId = request.UserID,
                    Message = status == "Approved"
                        ? $"📚 Approved: {request.Book.Title}\nPlease collect within 3 days (by {DateTime.Now.AddDays(3):MMM dd})"
                        : $"❌ Rejected: {request.Book.Title}",
                    Link = status == "Approved" ? "/Student/Dashboard" : "",
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(studentNotification);

                await _context.SaveChangesAsync();

                // Log the activity
                var currentUser = await _userManager.GetUserAsync(User);
                await _activityLogService.LogRequestAction(
                    currentUser.Id,
                    request.UserID,
                    request.BookID,
                    status == "Approved",
                    request.RequestID
                );

                // Send real-time notification
                await _hubContext.Clients.User(request.UserID)
                    .SendAsync("ReceiveNotification", new
                    {
                        message = studentNotification.Message,
                        isSuccess = status == "Approved"
                    });

                TempData["Success"] = status == "Approved" 
                    ? "Request approved successfully!" 
                    : "Request rejected successfully!";

                return RedirectToAction("PendingRequest");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateRequestStatus: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                TempData["Error"] = "An error occurred while processing the request.";
                return RedirectToAction("PendingRequest");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewBook(int id)
        {
            var book = await _context.Books
                .Include(b => b.BookSubjects)
                .ThenInclude(bs => bs.Subject)
                .Include(b => b.BookCopies)
                .FirstOrDefaultAsync(b => b.BookID == id);

            if (book == null)
            {
                return NotFound();
            }

            // Calculate total and available copies
            ViewBag.TotalCopies = book.BookCopies.Count;
            ViewBag.AvailableCopies = book.BookCopies.Count(c => c.IsAvailable);
            ViewBag.IsArchived = book.IsArchived; // Pass archive status to the view

            return View(book);
        }

        public async Task<IActionResult> EditBook(int id)
        {
            var book = await _context.Books
                .Include(b => b.BookSubjects)
                .ThenInclude(bs => bs.Subject)
                .Include(b => b.BookCopies)
                .FirstOrDefaultAsync(b => b.BookID == id);

            if (book == null)
            {
                return NotFound();
            }

            // Calculate total and available copies
            ViewBag.TotalCopies = book.BookCopies.Count;
            ViewBag.AvailableCopies = book.BookCopies.Count(c => c.IsAvailable);
            ViewBag.AllSubjects = await _context.Subjects.ToListAsync();
            return View(book);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBook(int id, Book book, List<int> selectedSubjectIds, int totalCopies)
        {
            if (id != book.BookID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingBook = await _context.Books
                        .Include(b => b.BookSubjects)
                        .Include(b => b.BookCopies)
                        .FirstOrDefaultAsync(b => b.BookID == id);

                    if (existingBook == null)
                    {
                        return NotFound();
                    }

                    // Check if availability status was changed
                    bool availabilityChanged = existingBook.IsAvailable != book.IsAvailable;
                    
                    // Update basic book information
                    existingBook.Title = book.Title;
                    existingBook.Author = book.Author;
                    existingBook.ISBN = book.ISBN;
                    existingBook.Description = book.Description;
                    existingBook.PublishedDate = book.PublishedDate;
                    existingBook.IsAvailable = book.IsAvailable;

                    // Update subjects
                    existingBook.BookSubjects.Clear();
                    if (selectedSubjectIds != null)
                    {
                        foreach (var subjectId in selectedSubjectIds)
                        {
                            existingBook.BookSubjects.Add(new BookSubject { SubjectId = subjectId });
                        }
                    }

                    // Handle book copies
                    var currentCopies = existingBook.BookCopies.Count;
                    var borrowedCopies = existingBook.BookCopies.Count(c => !c.IsAvailable);
                    
                    // Validate that we can't reduce below borrowed copies
                    if (totalCopies < borrowedCopies)
                    {
                        ModelState.AddModelError("totalCopies", $"Cannot reduce copies below {borrowedCopies} (currently borrowed copies).");
                        ViewBag.AllSubjects = await _context.Subjects.ToListAsync();
                        ViewBag.SelectedSubjectIds = selectedSubjectIds;
                        ViewBag.TotalCopies = currentCopies;
                        ViewBag.AvailableCopies = currentCopies - borrowedCopies;
                        return View(book);
                    }

                    if (totalCopies > currentCopies)
                    {
                        // Add new copies
                        for (int i = currentCopies + 1; i <= totalCopies; i++)
                        {
                            var copyNumber = $"{existingBook.BookNumber}CN{i}";
                            existingBook.BookCopies.Add(new BookCopy
                            {
                                BookID = existingBook.BookID,
                                CopyNumber = copyNumber,
                                IsAvailable = true
                            });
                        }
                    }
                    else if (totalCopies < currentCopies)
                    {
                        // Get available copies
                        var availableCopies = existingBook.BookCopies.Where(c => c.IsAvailable).ToList();
                        var copiesToRemove = currentCopies - totalCopies;

                        if (availableCopies.Count < copiesToRemove)
                        {
                            ModelState.AddModelError("", "Unable to remove enough copies, some are currently borrowed");
                            return View(book);
                        }

                        foreach (var copy in availableCopies.Take(copiesToRemove))
                        {
                            _context.BookCopies.Remove(copy);
                        }
                    }

                    await _context.SaveChangesAsync();

                    // Log the activity
                    var currentUser = await _userManager.GetUserAsync(User);
                    
                    // If availability changed, log only that specifically
                    if (availabilityChanged)
                    {
                        await _activityLogService.LogBookAvailabilityChanged(currentUser.Id, existingBook.BookID, existingBook.IsAvailable);
                    }
                    else
                    {
                        // Otherwise log general book update
                        await _activityLogService.LogBookUpdated(currentUser.Id, existingBook.BookID);
                    }

                    TempData["Success"] = "Book updated successfully!";
                    return RedirectToAction(nameof(ViewBook), new { id = existingBook.BookID });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Books.Any(e => e.BookID == book.BookID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            ViewBag.AllSubjects = await _context.Subjects.ToListAsync();
            return View(book);
        }

        [HttpGet]
        public async Task<IActionResult> BorrowerProfile(string searchTerm)
        {
            var borrowers = await _context.Users
                .Where(u => u.Role == "Student" && !u.IsArchived)
                .Where(u => string.IsNullOrEmpty(searchTerm) ||
                           (u.FirstName + " " + u.LastName).Contains(searchTerm) ||
                           u.Email.Contains(searchTerm))
                .Select(u => new BorrowerProfileViewModel
                {
                    UserId = u.Id,
                    FullName = u.FirstName + " " + u.LastName,
                    Email = u.Email,
                    CurrentBorrowings = _context.BorrowingHistories
                        .Include(b => b.Book)
                        .Where(b => b.UserID == u.Id && !b.IsReturned)
                        .ToList(),
                    BorrowingHistory = _context.BorrowingHistories
                        .Include(b => b.Book)
                        .Where(b => b.UserID == u.Id && b.IsReturned)
                        .ToList()
                })
                .ToListAsync();

            return View(borrowers);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsReturned(int borrowingId)
        {
            try
            {
                var borrowing = await _context.BorrowingHistories
                    .Include(b => b.Book)
                    .ThenInclude(b => b.BookCopies)
                    .Include(b => b.User)
                    .FirstOrDefaultAsync(b => b.BorrowingHistoryID == borrowingId);

                if (borrowing == null)
                {
                    return NotFound();
                }

                // Find and mark the specific copy as available
                var borrowedCopy = borrowing.Book.BookCopies
                    .FirstOrDefault(c => c.CopyID == borrowing.CopyID);

                if (borrowedCopy != null)
                {
                    borrowedCopy.IsAvailable = true;
                }
                else
                {
                    TempData["Error"] = "Could not find the borrowed copy.";
                    return RedirectToAction(nameof(BorrowerProfile));
                }

                borrowing.IsReturned = true;
                borrowing.ReturnDate = DateTime.Now;

                await _context.SaveChangesAsync();

                // Log the activity
                var currentUser = await _userManager.GetUserAsync(User);
                await _activityLogService.LogBookReturn(
                    currentUser.Id,
                    borrowing.UserID,
                    borrowing.BookID,
                    borrowing.BorrowingHistoryID
                );

                TempData["Success"] = "Book marked as returned successfully!";
                return RedirectToAction(nameof(BorrowerProfile));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MarkAsReturned: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                TempData["Error"] = "An error occurred while marking the book as returned.";
                return RedirectToAction(nameof(BorrowerProfile));
            }
        }

        public async Task<IActionResult> ViewProfile(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var userRole = roles.FirstOrDefault();

            // Get borrowing data
            var borrowings = await _context.BorrowingHistories
                .Include(b => b.Book)
                .Where(b => b.User.Id == user.Id)
                .OrderByDescending(b => b.BorrowDate)
                .ToListAsync();

            // Calculate most borrowed books
            var mostBorrowedBooks = borrowings
                .GroupBy(b => new { b.Book.BookID, b.Book.Title, b.Book.Author, b.Book.CoverImageUrl })
                .Select(g => new MostBorrowedBook
                {
                    BookId = g.Key.BookID,
                    Title = g.Key.Title,
                    Author = g.Key.Author,
                    CoverImageUrl = g.Key.CoverImageUrl,
                    BorrowCount = g.Count(),
                    LastBorrowed = g.Max(b => b.BorrowDate)
                })
                .Where(b => b.BorrowCount > 1) // Only include books borrowed more than once
                .OrderByDescending(b => b.BorrowCount)
                .ThenByDescending(b => b.LastBorrowed)
                .ToList();

            var viewModel = new UserProfileViewModel
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = userRole,
                LRN = user.LRN,
                Section = user.Section,
                YearLevel = user.YearLevel,
                CreatedAt = user.CreatedAt,
                ProfilePictureUrl = user.ProfilePictureUrl,
                CurrentBorrowings = borrowings.Where(b => !b.IsReturned).ToList(),
                BorrowingHistory = borrowings.Where(b => b.IsReturned).ToList(),
                MostBorrowedBooks = mostBorrowedBooks
            };

            return View("~/Views/Shared/Profile.cshtml", viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> ArchivedBooks()
        {
            var books = await _context.Books
                .Where(b => b.IsArchived) // Only show archived books
                .Include(b => b.BookSubjects)
                .ThenInclude(bs => bs.Subject)
                .OrderBy(b => b.Title)
                .ToListAsync();

            return View("~/Views/SystemAdmin/ArchivedBooks.cshtml", books);
        }

        [HttpGet]
        public async Task<IActionResult> ArchiveBook(int id)
        {
            var book = await _context.Books
                .Include(b => b.BookSubjects)
                .ThenInclude(bs => bs.Subject)
                .Include(b => b.BookCopies)
                .FirstOrDefaultAsync(b => b.BookID == id);

            if (book == null)
            {
                return NotFound();
            }

            // Check if any copies are currently borrowed
            bool anyBorrowed = await _context.BorrowingHistories
                .AnyAsync(bh => bh.BookID == id && bh.ReturnDate == null);

            if (anyBorrowed)
            {
                TempData["Error"] = "Cannot archive book with copies currently borrowed by users.";
                return RedirectToAction(nameof(ManageBooks));
            }

            return View("~/Views/SystemAdmin/ArchiveBook.cshtml", book);
        }

        [HttpPost, ActionName("ArchiveBook")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveBookConfirmed(int id)
        {
            var book = await _context.Books
                .FirstOrDefaultAsync(b => b.BookID == id);

            if (book == null)
            {
                return NotFound();
            }

            try
            {
                // Check if any copies are currently borrowed
                bool anyBorrowed = await _context.BorrowingHistories
                    .AnyAsync(bh => bh.BookID == id && bh.ReturnDate == null);

                if (anyBorrowed)
                {
                    TempData["Error"] = "Cannot archive book with copies currently borrowed by users.";
                    return RedirectToAction(nameof(ManageBooks));
                }

                // Archive the book
                book.IsArchived = true;
                await _context.SaveChangesAsync();

                // Log the activity
                var currentUser = await _userManager.GetUserAsync(User);
                await _activityLogService.LogActivity("BookArchived", $"Archived book: {book.Title}", currentUser.Id, null, book.BookID);
                
                TempData["Success"] = "Book archived successfully.";
                return RedirectToAction(nameof(ManageBooks));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while archiving the book: {ex.Message}";
                return RedirectToAction(nameof(ManageBooks));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreBook(int id)
        {
            var book = await _context.Books
                .FirstOrDefaultAsync(b => b.BookID == id);

            if (book == null)
            {
                return NotFound();
            }

            book.IsArchived = false;
            await _context.SaveChangesAsync();

            // Log the activity
            var currentUser = await _userManager.GetUserAsync(User);
            await _activityLogService.LogActivity("BookRestored", $"Restored book: {book.Title}", currentUser.Id, null, book.BookID);

            TempData["Success"] = "Book restored successfully.";
            return RedirectToAction(nameof(ArchivedBooks));
        }

        [HttpGet]
        public IActionResult CreateUser()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Validate that the role is either Student, Teacher, or Staff
                if (model.Role != "Student" && model.Role != "Teacher" && model.Role != "Staff")
                {
                    ModelState.AddModelError("Role", "Librarians can only create Student, Teacher, or Staff accounts.");
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Role = model.Role,
                    LRN = model.LRN,
                    Section = model.Section,
                    YearLevel = model.YearLevel,
                    ProfilePictureUrl = "/images/DefProfPic.jpg",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, model.Role);
                    
                    // Log the user creation activity
                    var currentUser = await _userManager.GetUserAsync(User);
                    await _activityLogService.LogActivity(
                        ActivityType.UserCreated,
                        $"Created new {model.Role} account: {model.FirstName} {model.LastName}",
                        currentUser.Id,
                        user.Id
                    );
                    
                    TempData["Success"] = "User created successfully!";
                    return RedirectToAction("ManageUsers");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
        }

        public async Task<IActionResult> ManageUsers(string searchTerm)
        {
            var users = await _context.Users
                .Where(u => (u.Role == "Student" || u.Role == "Teacher" || u.Role == "Staff") && 
                           !u.IsArchived &&
                           (string.IsNullOrEmpty(searchTerm) || 
                            (u.FirstName + " " + u.LastName).Contains(searchTerm) ||
                            u.Email.Contains(searchTerm)))
                .ToListAsync();

            var userViewModels = users.Select(u => new UserViewModel
            {
                UserId = u.Id,
                FullName = u.FirstName + " " + u.LastName,
                Email = u.Email,
                Role = u.Role,
                CreatedAt = u.CreatedAt
            }).ToList();

            return View(userViewModels);
        }

        [HttpGet]
        public async Task<IActionResult> CheckLRNAvailability(string lrn)
        {
            // Check if the LRN is null or empty
            if (string.IsNullOrEmpty(lrn))
            {
                return Json(new { exists = false });
            }

            // Check if any non-archived user (especially students) has this LRN
            bool lrnExists = await _context.Users
                .Where(u => !u.IsArchived && u.LRN == lrn)
                .AnyAsync();

            return Json(new { exists = lrnExists });
        }
    }
}