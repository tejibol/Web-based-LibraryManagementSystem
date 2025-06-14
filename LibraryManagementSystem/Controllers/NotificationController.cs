using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using Microsoft.AspNetCore.Identity;

public class NotificationController : Controller
{
    private readonly ApplicationDbContext _context;

    public NotificationController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> MarkAsReadAndRedirect(int id)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null) return NotFound();

        // Mark as read
        notification.IsRead = true;
        await _context.SaveChangesAsync();

        // Redirect using the Link property
        if (!string.IsNullOrEmpty(notification.Link))
        {
            return LocalRedirect(notification.Link); // Using LocalRedirect for security
        }

        // Fallback redirection based on user role
        if (User.IsInRole("Librarian"))
        {
            return RedirectToAction("PendingRequest", "Librarian");
        }
        else if (User.IsInRole("Student"))
        {
            return RedirectToAction("Dashboard", "Student");
        }

        return RedirectToAction("Index", "Home");
    }
}