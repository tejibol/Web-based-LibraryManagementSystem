using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data; // palitan mo 'to sa actual namespace mo
using LibraryManagementSystem.Models; // kung saan yung Notification model mo
using Microsoft.AspNetCore.Identity;

public class NotificationBellViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationBellViewComponent(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await _userManager.GetUserAsync(HttpContext.User);
        if (user == null) return View(new List<Notification>());

        var userRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

        var notifs = await _context.Notifications
            .Where(n =>
                (n.UserId == user.Id) ||
                (n.TargetRole == userRole && n.UserId == null))
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return View("_NotificationPartial", notifs);
    }
}