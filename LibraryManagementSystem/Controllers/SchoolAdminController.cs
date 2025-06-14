using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace LibraryManagementSystem.Controllers
{
    public class SchoolAdminController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ActivityLogService _activityLogService;

        public SchoolAdminController(
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
        public async Task<IActionResult> Dashboard()
        {
            // Get total number of students
            ViewBag.TotalStudents = await _userManager.GetUsersInRoleAsync("Student");

            // Get total number of books (excluding archived)
            ViewBag.TotalBooks = await _context.Books.Where(b => !b.IsArchived).CountAsync();

            // Get total currently borrowed books
            ViewBag.CurrentlyBorrowed = await _context.BorrowingHistories
                .Where(b => b.ReturnDate == null)
                .CountAsync();

            // Get total overdue books
            ViewBag.OverdueBooks = await _context.BorrowingHistories
                .Where(b => b.ReturnDate == null && b.DueDate < DateTime.Now)
                .CountAsync();

            // Add total book copies count (excluding archived books)
            ViewBag.TotalBookCopies = await _context.BookCopies
                .Include(c => c.Book)
                .Where(c => !c.Book.IsArchived)
                .CountAsync();

            return View();
        }
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");  // Redirect to Home page or Login page
        }

        public async Task<IActionResult> ManageUsers(string searchTerm)
        {
            var users = await _context.Users
                .Where(u => (u.Role == "Student" || u.Role == "Librarian" || u.Role == "Staff") && 
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
        public IActionResult CreateUser()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Validate that the role is either Student, Librarian, or Staff
                if (model.Role != "Student" && model.Role != "Librarian" && model.Role != "Staff")
                {
                    ModelState.AddModelError("Role", "Invalid role selected.");
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
                    ProfilePictureUrl = "/images/DefProfPic.jpg"
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, model.Role);
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

        public async Task<IActionResult> ActivityLog(string searchTerm, string activityType, DateTime? startDate, DateTime? endDate, string userRole)
        {
            var query = _context.ActivityLogs
                .Include(a => a.User)
                .Include(a => a.AffectedUser)
                .Include(a => a.Book)
                .Where(a => a.User.Role == "Librarian" || a.User.Role == "Student") // Only show activities from Librarians and Students
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

            // Apply role filter if specified, restricting to only Student or Librarian
            if (!string.IsNullOrEmpty(userRole))
            {
                if (userRole == "Borrower")
                {
                    // For SchoolAdmin, Borrower means Student only
                    query = query.Where(a => a.User.Role == "Student");
                }
                else if (userRole == "Librarian")
                {
                    query = query.Where(a => a.User.Role == "Librarian");
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

            // Add filter options to ViewBag - only include activity types that students and librarians would perform
            ViewBag.ActivityTypes = new[]
            {
                ActivityType.BorrowRequest,     // Student activity
                ActivityType.BookReturned,      // Librarian and Student activity
                ActivityType.BookOverdue,       // Student activity
                ActivityType.BookAdded,         // Librarian activity
                ActivityType.BookUpdated,       // Librarian activity
                ActivityType.BookAvailabilityChanged, // Librarian activity
                ActivityType.UserCreated        // Librarian activity
            };

            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedActivityType = activityType;
            ViewBag.SelectedRole = userRole;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            return View(logs);
        }

        // New action to archive a user
        [HttpPost]
        public async Task<IActionResult> ArchiveUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            
            // Ensure we can only archive students and librarians
            if (user.Role != "Student" && user.Role != "Librarian")
            {
                TempData["Error"] = "You can only archive Student and Librarian accounts.";
                return RedirectToAction("ManageUsers");
            }
            
            // Check if the user has any active borrowings
            var hasActiveBorrowings = await _context.BorrowingHistories
                .AnyAsync(bh => bh.UserID == id && bh.ReturnDate == null);
                
            if (hasActiveBorrowings)
            {
                TempData["Error"] = "Cannot archive a user with active borrowings. All books must be returned first.";
                return RedirectToAction("ManageUsers");
            }
            
            user.IsArchived = true;
            await _userManager.UpdateAsync(user);
            
            // Log the activity
            await _activityLogService.LogActivity(
                ActivityType.UserArchived, 
                $"Archived user account: {user.FirstName} {user.LastName}", 
                (await _userManager.GetUserAsync(User)).Id, 
                user.Id);
                
            TempData["Success"] = "User archived successfully.";
            return RedirectToAction("ManageUsers");
        }

        // New action to view archived users
        public async Task<IActionResult> ArchivedUsers()
        {
            var archivedUsers = await _context.Users
                .Where(u => u.IsArchived && (u.Role == "Student" || u.Role == "Librarian"))
                .ToListAsync();

            var userViewModels = archivedUsers.Select(u => new UserViewModel
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
            
            // Ensure we can only unarchive students and librarians
            if (user.Role != "Student" && user.Role != "Librarian")
            {
                TempData["Error"] = "You can only unarchive Student and Librarian accounts.";
                return RedirectToAction("ArchivedUsers");
            }
            
            user.IsArchived = false;
            await _userManager.UpdateAsync(user);
            
            // Log the activity
            await _activityLogService.LogActivity(
                ActivityType.UserUnarchived, 
                $"Unarchived user account: {user.FirstName} {user.LastName}", 
                (await _userManager.GetUserAsync(User)).Id, 
                user.Id);
                
            TempData["Success"] = "User unarchived successfully.";
            return RedirectToAction("ArchivedUsers");
        }

        // Method to advance student year levels at the end of the school year
        [HttpGet]
        public async Task<IActionResult> AdvanceYearLevels()
        {
            // Get all non-archived students
            var studentsToAdvance = await _userManager.GetUsersInRoleAsync("Student");
            studentsToAdvance = studentsToAdvance.Where(s => !s.IsArchived).ToList();
            
            return View(studentsToAdvance);
        }
        
        [HttpPost]
        public async Task<IActionResult> AdvanceYearLevelsConfirm(bool confirmAdvancement, List<string> excludeStudents = null)
        {
            if (!confirmAdvancement)
            {
                TempData["Info"] = "Year level advancement was cancelled.";
                return RedirectToAction("Dashboard");
            }
            
            // Get all non-archived students
            var studentsToAdvance = await _userManager.GetUsersInRoleAsync("Student");
            studentsToAdvance = studentsToAdvance.Where(s => !s.IsArchived).ToList();
            
            // Exclude specified students if any
            if (excludeStudents != null && excludeStudents.Any())
            {
                studentsToAdvance = studentsToAdvance.Where(s => !excludeStudents.Contains(s.Id)).ToList();
            }
            
            int advancedCount = 0;
            int graduatedCount = 0;
            
            foreach (var student in studentsToAdvance)
            {
                if (string.IsNullOrEmpty(student.YearLevel))
                    continue;
                    
                // Currently, we're checking for "Grade X" format
                if (student.YearLevel.StartsWith("Grade "))
                {
                    string gradeNumberStr = student.YearLevel.Substring(6);
                    if (int.TryParse(gradeNumberStr, out int gradeNumber))
                    {
                        if (gradeNumber < 10) // Assuming Grade 10 is the highest
                        {
                            // Advance to next grade
                            student.YearLevel = $"Grade {gradeNumber + 1}";
                            
                            // Update section based on section ID
                            if (!string.IsNullOrEmpty(student.Section))
                            {
                                // Check if the section follows our pattern: GX-SY-Name
                                if (student.Section.StartsWith("G") && student.Section.Contains("-S"))
                                {
                                    // Extract the section number
                                    string[] parts = student.Section.Split('-');
                                    if (parts.Length >= 2 && parts[1].StartsWith("S"))
                                    {
                                        string sectionNumber = parts[1]; // e.g., "S1"
                                        
                                        // Map the section number to the corresponding section in the new grade
                                        switch (gradeNumber + 1)
                                        {
                                            case 8:
                                                // Map to Grade 8 section with same number
                                                switch (sectionNumber)
                                                {
                                                    case "S1":
                                                        student.Section = "G8-S1-Rizal";
                                                        break;
                                                    case "S2":
                                                        student.Section = "G8-S2-Bonifacio";
                                                        break;
                                                    case "S3":
                                                        student.Section = "G8-S3-Mabini";
                                                        break;
                                                    case "S4":
                                                        student.Section = "G8-S4-Luna";
                                                        break;
                                                    case "S5":
                                                        student.Section = "G8-S5-Silang";
                                                        break;
                                                    default:
                                                        student.Section = "G8-S1-Rizal";
                                                        break;
                                                }
                                                break;
                                                
                                            case 9:
                                                // Map to Grade 9 section with same number
                                                switch (sectionNumber)
                                                {
                                                    case "S1":
                                                        student.Section = "G9-S1-Sampaguita";
                                                        break;
                                                    case "S2":
                                                        student.Section = "G9-S2-Gumamela";
                                                        break;
                                                    case "S3":
                                                        student.Section = "G9-S3-Ilang-Ilang";
                                                        break;
                                                    case "S4":
                                                        student.Section = "G9-S4-Camia";
                                                        break;
                                                    case "S5":
                                                        student.Section = "G9-S5-Rosal";
                                                        break;
                                                    default:
                                                        student.Section = "G9-S1-Sampaguita";
                                                        break;
                                                }
                                                break;
                                                
                                            case 10:
                                                // Map to Grade 10 section with same number
                                                switch (sectionNumber)
                                                {
                                                    case "S1":
                                                        student.Section = "G10-S1-Sapphire";
                                                        break;
                                                    case "S2":
                                                        student.Section = "G10-S2-Emerald";
                                                        break;
                                                    case "S3":
                                                        student.Section = "G10-S3-Ruby";
                                                        break;
                                                    case "S4":
                                                        student.Section = "G10-S4-Diamond";
                                                        break;
                                                    case "S5":
                                                        student.Section = "G10-S5-Pearl";
                                                        break;
                                                    default:
                                                        student.Section = "G10-S1-Sapphire";
                                                        break;
                                                }
                                                break;
                                        }
                                    }
                                }
                            }
                            
                            advancedCount++;
                        }
                        else
                        {
                            // Student has completed Grade 10 - mark as graduated or take appropriate action
                            // For now, we'll just archive them
                            student.IsArchived = true;
                            graduatedCount++;
                            
                            // Log graduation activity
                            await _activityLogService.LogActivity(
                                ActivityType.UserArchived,
                                $"Student graduated: {student.FirstName} {student.LastName}",
                                (await _userManager.GetUserAsync(User)).Id,
                                student.Id);
                        }
                        
                        // Save changes for this student
                        await _userManager.UpdateAsync(student);
                    }
                }
            }
            
            // Log the bulk activity
            await _activityLogService.LogActivity(
                ActivityType.SystemMaintenance,
                $"Advanced year levels: {advancedCount} students advanced, {graduatedCount} students graduated",
                (await _userManager.GetUserAsync(User)).Id);
                
            TempData["Success"] = $"Successfully advanced {advancedCount} students and graduated {graduatedCount} students.";
            return RedirectToAction("Dashboard");
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

            // Ensure we can only edit students and librarians
            if (user.Role != "Student" && user.Role != "Librarian")
            {
                TempData["Error"] = "You can only edit Student and Librarian accounts.";
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
                // First, get the user from the database
                var user = await _userManager.FindByIdAsync(model.Id);
                if (user == null)
                {
                    return NotFound();
                }

                // Ensure we can only edit students and librarians
                if (user.Role != "Student" && user.Role != "Librarian")
                {
                    TempData["Error"] = "You can only edit Student and Librarian accounts.";
                    return RedirectToAction("ManageUsers");
                }

                // Create a flag to track if any changes were made
                bool modified = false;

                // For student accounts, update both YearLevel and Section
                if (user.Role == "Student")
                {
                    // Update section if provided
                    if (!string.IsNullOrEmpty(model.Section) && user.Section != model.Section)
                    {
                        user.Section = model.Section;
                        modified = true;
                        
                        // Log the section change for debugging
                        Console.WriteLine($"DEBUG: Updating section for student {user.Id} from '{user.Section}' to '{model.Section}'");
                    }
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
                    
                    modified = true;
                }

                // Only update the database if changes were made
                if (modified)
                {
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

                    // Create a detailed description of what was updated
                    string updateDescription = user.Role == "Student" 
                        ? $"Updated student: {user.FirstName} {user.LastName} (Section: {user.Section})"
                        : $"Updated user: {user.FirstName} {user.LastName}";
                    
                    // Log the activity
                    await _activityLogService.LogActivity(
                        ActivityType.UserEdited,
                        updateDescription,
                        (await _userManager.GetUserAsync(User)).Id,
                        user.Id);

                    TempData["Success"] = "User updated successfully.";
                }
                else
                {
                    TempData["Info"] = "No changes were made to the user.";
                }
                
                // If the user came from ManageStudents, redirect back there
                if (Request.Headers["Referer"].ToString().Contains("ManageStudents"))
                {
                    return RedirectToAction("ManageStudents");
                }
                
                return RedirectToAction("ManageUsers");
            }

            return View(model);
        }

        // New method to manage students by year level and section
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
