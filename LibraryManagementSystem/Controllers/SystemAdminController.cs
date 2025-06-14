using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Microsoft.AspNetCore.Http;
using LibraryManagementSystem.Services;
using System.Diagnostics;

namespace LibraryManagementSystem.Controllers
{
    [Authorize(Roles = "SystemAdmin")]
    public class SystemAdminController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ActivityLogService _activityLogService;

        public SystemAdminController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ActivityLogService activityLogService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _activityLogService = activityLogService;
        }

        public IActionResult Dashboard()
        {
            // Get statistics
            ViewBag.TotalUsers = _userManager.Users.Count();
            ViewBag.TotalBooks = _context.Books.Where(b => !b.IsArchived).Count();
            ViewBag.OverdueBooks = _context.BorrowingHistories
            .Where(b => b.ReturnDate == null && b.DueDate < DateTime.Now)
            .Count();
            
            // Add total book copies count (excluding archived books)
            ViewBag.TotalBookCopies = _context.BookCopies
                .Include(c => c.Book)
                .Where(c => !c.Book.IsArchived)
                .Count();

            return View();
        }

        public async Task<IActionResult> ManageUsers(string searchTerm)
        {
            var users = await _context.Users
                .Where(u => !u.IsArchived && (string.IsNullOrEmpty(searchTerm) || (u.FirstName + " " + u.LastName).Contains(searchTerm)))
                .ToListAsync();

            var filteredUsers = new List<ApplicationUser>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Contains("SystemAdmin"))
                {
                    filteredUsers.Add(user);
                }
            }

            var userViewModels = filteredUsers.Select(u => new UserViewModel
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
        public IActionResult CreateUser()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Role = model.Role, // for display
                    LRN = model.LRN,
                    Section = model.Section,
                    YearLevel = model.YearLevel,
                    ProfilePictureUrl = "/images/DefProfPic.jpg" // Set default profile picture
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, model.Role); // actual Identity role
                    return RedirectToAction("ManageUsers"); // or wherever you list users
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
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
        public async Task<IActionResult> EditUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Don't allow editing system admin users (they should use profile page instead)
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("SystemAdmin"))
            {
                TempData["Error"] = "System Admin accounts cannot be edited from this interface.";
                return RedirectToAction("ManageUsers");
            }

            var model = new EditUserViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                LRN = user.LRN,
                Section = user.Section,
                YearLevel = user.YearLevel
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(model.Id);
                if (user == null)
                {
                    return NotFound();
                }

                // Don't allow editing system admin users
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("SystemAdmin"))
                {
                    TempData["Error"] = "System Admin accounts cannot be edited from this interface.";
                    return RedirectToAction("ManageUsers");
                }

                // Only update the Section field for Students
                if (user.Role == "Student")
                {
                    user.Section = model.Section;
                }

                // Update password if provided
                if (!string.IsNullOrEmpty(model.Password))
                {
                    // Remove the current password and add the new one
                    var removePasswordResult = await _userManager.RemovePasswordAsync(user);
                    if (!removePasswordResult.Succeeded)
                    {
                        foreach (var error in removePasswordResult.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                        return View(model);
                    }

                    var addPasswordResult = await _userManager.AddPasswordAsync(user, model.Password);
                    if (!addPasswordResult.Succeeded)
                    {
                        foreach (var error in addPasswordResult.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                        return View(model);
                    }
                }

                // Update the user in the database
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    foreach (var error in updateResult.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    return View(model);
                }

                // Log the activity
                await _activityLogService.LogActivity(
                    ActivityType.UserEdited,
                    $"Updated section for student: {user.FirstName} {user.LastName}",
                    (await _userManager.GetUserAsync(User)).Id,
                    user.Id);

                TempData["Success"] = "User updated successfully.";
                return RedirectToAction("ManageUsers");
            }

            return View(model);
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

            return View("~/Views/Shared/ViewBook.cshtml", book);
        }

        [HttpGet]
        public async Task<IActionResult> AddBook()
        {
            ViewBag.AllSubjects = await _context.Subjects.ToListAsync();
            return View("~/Views/Librarian/AddBook.cshtml");
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
                        return View("~/Views/Librarian/AddBook.cshtml", book);
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
            return View("~/Views/Shared/AddBook.cshtml", book);
        }

        [HttpGet]
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
            ViewBag.SelectedSubjectIds = book.BookSubjects.Select(bs => bs.SubjectId).ToList();
            return View("~/Views/Librarian/EditBook.cshtml", book);
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
                    _context.BookSubjects.RemoveRange(existingBook.BookSubjects);
                    if (selectedSubjectIds != null && selectedSubjectIds.Any())
                    {
                        foreach (var subjectId in selectedSubjectIds)
                        {
                            existingBook.BookSubjects.Add(new BookSubject { SubjectId = subjectId, BookId = existingBook.BookID });
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("selectedSubjectIds", "Please select at least one subject");
                        ViewBag.AllSubjects = await _context.Subjects.ToListAsync();
                        ViewBag.SelectedSubjectIds = selectedSubjectIds;
                        ViewBag.TotalCopies = existingBook.BookCopies.Count;
                        ViewBag.AvailableCopies = existingBook.BookCopies.Count(c => c.IsAvailable);
                        return View("~/Views/Shared/EditBook.cshtml", book);
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
                        return View("~/Views/Shared/EditBook.cshtml", book);
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
                        // Get available copies ordered by copy number (to remove newest copies first)
                        var availableCopies = existingBook.BookCopies
                            .Where(c => c.IsAvailable)
                            .OrderByDescending(c => c.CopyNumber)
                            .ToList();

                        // Calculate how many copies we need to remove
                        var copiesToRemove = currentCopies - totalCopies;

                        // Remove the specified number of available copies
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
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error: {ex.Message}");
                    TempData["Error"] = "Failed to update book. Please try again.";
                }
            }

            ViewBag.AllSubjects = await _context.Subjects.ToListAsync();
            ViewBag.SelectedSubjectIds = selectedSubjectIds;
            return View("~/Views/Shared/EditBook.cshtml", book);
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

            return View(book);
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

        // Add an action to view archived books
        [HttpGet]
        public async Task<IActionResult> ArchivedBooks()
        {
            var books = await _context.Books
                .Where(b => b.IsArchived) // Only show archived books
                .Include(b => b.BookSubjects)
                .ThenInclude(bs => bs.Subject)
                .OrderBy(b => b.Title)
                .ToListAsync();

            return View(books);
        }

        // Add an action to restore an archived book
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

        // New action to archive a user
        [HttpPost]
        public async Task<IActionResult> ArchiveUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            
            // Prevent archiving SystemAdmin users
            if (user.Role == "SystemAdmin")
            {
                return RedirectToAction("ManageUsers");
            }
            
            user.IsArchived = true;
            await _userManager.UpdateAsync(user);
            return RedirectToAction("ManageUsers");
        }

        // New action to view archived users
        public async Task<IActionResult> ArchivedUsers()
        {
            var archivedUsers = await _context.Users
                .Where(u => u.IsArchived)
                .ToListAsync();

            var filteredUsers = new List<ApplicationUser>();
            foreach (var user in archivedUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Contains("SystemAdmin"))
                {
                    filteredUsers.Add(user);
                }
            }

            var userViewModels = filteredUsers.Select(u => new UserViewModel
            {
                UserId = u.Id,
                FullName = u.FirstName + " " + u.LastName,
                Email = u.Email,
                Role = u.Role,
                CreatedAt = u.CreatedAt
            }).ToList();
            return View(userViewModels);
        }

        // New action to unarchive a user
        [HttpPost]
        public async Task<IActionResult> UnarchiveUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            user.IsArchived = false;
            await _userManager.UpdateAsync(user);
            return RedirectToAction("ArchivedUsers");
        }

        // Logout Action
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");  // Redirect to Home page or Login page
        }

        public async Task<IActionResult> ActivityLog(string searchTerm, string activityType, DateTime? startDate, DateTime? endDate, string userRole)
        {
            var query = _context.ActivityLogs
                .Include(a => a.User)
                .Include(a => a.AffectedUser)
                .Include(a => a.Book)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(a =>
                    a.Description.Contains(searchTerm) ||
                    a.User.FirstName.Contains(searchTerm) ||
                    a.User.LastName.Contains(searchTerm) ||
                    (a.AffectedUser != null && (a.AffectedUser.FirstName.Contains(searchTerm) || a.AffectedUser.LastName.Contains(searchTerm))) ||
                    (a.Book != null && a.Book.Title.Contains(searchTerm))
                );
            }

            if (!string.IsNullOrEmpty(activityType))
            {
                query = query.Where(a => a.ActivityType == activityType);
            }

            // Apply role filter if specified
            if (!string.IsNullOrEmpty(userRole))
            {
                if (userRole == "Borrower")
                {
                    // Borrower includes both Student and Teacher roles
                    query = query.Where(a => a.User.Role == "Student" || a.User.Role == "Teacher");
                }
                else
                {
                    query = query.Where(a => a.User.Role == userRole);
                }
            }

            if (startDate.HasValue)
            {
                query = query.Where(a => a.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.Timestamp <= endDate.Value.AddDays(1));
            }

            // Get the logs and convert to view models
            var logs = await query
                .OrderByDescending(a => a.Timestamp)
                .Select(a => new ActivityLogViewModel
                {
                    ActivityLogID = a.ActivityLogID,
                    ActivityType = a.ActivityType,
                    Description = a.Description,
                    PerformedBy = $"{a.User.FirstName} {a.User.LastName}",
                    AffectedUser = a.AffectedUser != null ? $"{a.AffectedUser.FirstName} {a.AffectedUser.LastName}" : null,
                    BookTitle = a.Book != null ? a.Book.Title : null,
                    Timestamp = a.Timestamp,
                    AdditionalInfo = a.AdditionalInfo
                })
                .ToListAsync();

            // Add filter options to ViewBag - include all activity types
            ViewBag.ActivityTypes = new[]
            {
                ActivityType.BorrowRequest,
                ActivityType.RequestApproved,
                ActivityType.RequestRejected,
                ActivityType.BookReturned,
                ActivityType.BookOverdue,
                ActivityType.BookAdded,
                ActivityType.BookUpdated,
                ActivityType.BookAvailabilityChanged,
                ActivityType.UserArchived,
                ActivityType.UserUnarchived
            };

            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedActivityType = activityType;
            ViewBag.SelectedRole = userRole;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            return View(logs);
        }

        // New action to manage students by year level and section
        public async Task<IActionResult> ManageStudents()
        {
            // Get all non-archived students
            var students = await _userManager.GetUsersInRoleAsync("Student");
            students = students.Where(s => !s.IsArchived).ToList();

            // Group students by Year Level and then by Section
            var groupedStudents = students
                .GroupBy(s => s.YearLevel)
                .OrderBy(g => g.Key) // Order by year level
                .Select(g => new YearLevelViewModel
                {
                    YearLevel = g.Key,
                    Sections = g
                        .GroupBy(s => s.Section)
                        .OrderBy(sg => sg.Key) // Order by section
                        .Select(sg => new SectionViewModel
                        {
                            SectionName = sg.Key,
                            Students = sg.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToList()
                        })
                        .ToList()
                })
                .ToList();

            return View(groupedStudents);
        }
    }
}
