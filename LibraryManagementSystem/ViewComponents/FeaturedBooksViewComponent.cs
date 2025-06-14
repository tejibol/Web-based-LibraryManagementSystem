using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;

namespace LibraryManagementSystem.ViewComponents
{
    public class FeaturedBooksViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public FeaturedBooksViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var featuredBooks = await _context.Books
                .Where(b => !b.IsArchived)
                .Include(b => b.BookCopies)
                .OrderByDescending(b => b.PublishedDate)
                .Take(4)
                .ToListAsync();

            return View(featuredBooks);
        }
    }
} 