using System.Diagnostics;
using LibraryManagementSystem.Data;
using Microsoft.AspNetCore.Mvc;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore; // Add this for Include() and ToListAsync()
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace LibraryManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context; // Add this for database access
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILogger<HomeController> logger) // Inject the DbContext and ILogger
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string searchTerm = "")
        {
            // First check if user is signed in (existing logic)
            if (_signInManager.IsSignedIn(User))
            {
                var user = await _userManager.GetUserAsync(User);

                if (user == null)
                {
                    await _signInManager.SignOutAsync();
                    return RedirectToAction("Index", "Home");
                }

                // Redirect based on role (existing logic)
                if (await _userManager.IsInRoleAsync(user, "SystemAdmin"))
                    return RedirectToAction("Dashboard", "SystemAdmin");
                else if (await _userManager.IsInRoleAsync(user, "SchoolAdmin"))
                    return RedirectToAction("Dashboard", "SchoolAdmin");
                else if (await _userManager.IsInRoleAsync(user, "Librarian"))
                    return RedirectToAction("Dashboard", "Librarian");
                else if (await _userManager.IsInRoleAsync(user, "Student"))
                    return RedirectToAction("Dashboard", "Student");
            }
            List<Book> books;
            if (!string.IsNullOrEmpty(searchTerm))
            {
                books = await _context.Books
                    .Where(b => !b.IsArchived && (
                              b.Title.Contains(searchTerm) ||
                              b.Author.Contains(searchTerm) ||
                              b.ISBN.Contains(searchTerm)))
                    .Include(b => b.BookSubjects)
                        .ThenInclude(bs => bs.Subject)
                    .ToListAsync();
            }
            else
            {
                books = await _context.Books
                    .Where(b => !b.IsArchived)
                    .OrderByDescending(b => b.BookID)
                    .Take(3)
                    .Include(b => b.BookSubjects)
                        .ThenInclude(bs => bs.Subject)
                    .ToListAsync();
            }

            ViewBag.Books = books;
            ViewBag.IsSearch = !string.IsNullOrEmpty(searchTerm);
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(string email, string password)
        {
            // Check if user exists and is not archived
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || user.IsArchived)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt or account is archived.");
                
                // Reload books even on failed login
                var latestBooks = await _context.Books
                    .Where(b => !b.IsArchived)
                    .OrderByDescending(b => b.BookID)
                    .Take(3)
                    .Include(b => b.BookSubjects)
                        .ThenInclude(bs => bs.Subject)
                    .ToListAsync();

                ViewBag.LatestBooks = latestBooks;
                return View();
            }
            
            // Existing login logic
            var result = await _signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                if (await _userManager.IsInRoleAsync(user, "SystemAdmin"))
                    return RedirectToAction("Dashboard", "SystemAdmin");
                else if (await _userManager.IsInRoleAsync(user, "SchoolAdmin"))
                    return RedirectToAction("Dashboard", "SchoolAdmin");
                else if (await _userManager.IsInRoleAsync(user, "Librarian"))
                    return RedirectToAction("Dashboard", "Librarian");
                else if (await _userManager.IsInRoleAsync(user, "Student"))
                    return RedirectToAction("Dashboard", "Student");
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");

            // Reload books even on failed login
            var books = await _context.Books
                .Where(b => !b.IsArchived)
                .OrderByDescending(b => b.BookID)
                .Take(3)
                .Include(b => b.BookSubjects)
                    .ThenInclude(bs => bs.Subject)
                .ToListAsync();

            ViewBag.LatestBooks = books;
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public async Task<IActionResult> ViewProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

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
                Role = roles.FirstOrDefault(),
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

        [HttpPost]
        public async Task<IActionResult> UploadProfilePicture(IFormFile profilePicture)
        {
            if (profilePicture == null || profilePicture.Length == 0)
            {
                return RedirectToAction("ViewProfile");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (await _userManager.IsInRoleAsync(user, "SystemAdmin"))
            {
                return RedirectToAction("ViewProfile");
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + profilePicture.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await profilePicture.CopyToAsync(fileStream);
            }

            user.ProfilePictureUrl = "/uploads/" + uniqueFileName;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                return RedirectToAction("ViewProfile");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Failed to update profile picture.");
                return RedirectToAction("ViewProfile");
            }
        }

        public async Task<IActionResult> BookCollection(string searchTerm, string author, string subject)
        {
            var booksQuery = _context.Books
                .Where(b => !b.IsArchived) // Exclude archived books
                .Include(b => b.BookSubjects)
                .ThenInclude(bs => bs.Subject)
                .AsQueryable();

            // Apply search filters
            if (!string.IsNullOrEmpty(searchTerm))
            {
                booksQuery = booksQuery.Where(b =>
                    b.Title.Contains(searchTerm) ||
                    b.Author.Contains(searchTerm) ||
                    b.ISBN.Contains(searchTerm) ||
                    b.Description.Contains(searchTerm));
            }

            if (!string.IsNullOrEmpty(author))
            {
                booksQuery = booksQuery.Where(b => b.Author.Contains(author));
            }

            if (!string.IsNullOrEmpty(subject))
            {
                booksQuery = booksQuery.Where(b => b.BookSubjects.Any(bs => bs.Subject.SubjectName.Contains(subject)));
            }

            // Get all unique authors for filter dropdown (exclude archived books)
            ViewBag.Authors = await _context.Books
                .Where(b => !b.IsArchived)
                .Select(b => b.Author)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            // Get all subjects for filter dropdown
            ViewBag.Subjects = await _context.Subjects
                .OrderBy(s => s.SubjectName)
                .ToListAsync();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedAuthor = author;
            ViewBag.SelectedSubject = subject;

            var books = await booksQuery
                .OrderBy(b => b.Title)
                .ToListAsync();

            return View(books);
        }

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

            // Calculate available copies
            ViewBag.AvailableCopies = book.BookCopies.Count(c => c.IsAvailable);
            ViewBag.TotalCopies = book.BookCopies.Count;
            ViewBag.IsArchived = book.IsArchived;

            return View(book);
        }
    }
}