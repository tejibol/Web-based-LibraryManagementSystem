using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using LibraryManagementSystem.Hubs;
using LibraryManagementSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, NameUserIdProvider>();

builder.Services.AddScoped<ActivityLogService>();
var app = builder.Build();

// ===== Role + Admin Seeding =====
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var dbContext = services.GetRequiredService<ApplicationDbContext>();

    try
    {
        // Create roles if they don't exist
        string[] roleNames = { "SystemAdmin", "SchoolAdmin", "Librarian", "Student", "Teacher", "Staff" };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // Create default admin
        var adminEmail = "admin@library.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Admin",
                LastName = "User",
                Role = "SystemAdmin",
                IsArchived = false,
                EmailConfirmed = true,
                DateOfBirth = new DateTime(1990, 1, 1),
                ProfilePictureUrl = "/images/default-profile.png",
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(adminUser, "Admin@123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "SystemAdmin");
                await dbContext.SaveChangesAsync(); // Ensure changes are saved
                Console.WriteLine("Admin user created successfully!");
            }
            else
            {
                var errors = string.Join("\n", result.Errors.Select(e => e.Description));
                Console.WriteLine($"Failed to create admin user. Errors:\n{errors}");
            }
        }
        else
        {
            // Update admin user if it exists
            adminUser.EmailConfirmed = true;
            adminUser.Role = "SystemAdmin";
            adminUser.IsArchived = false;
            
            if (adminUser.DateOfBirth == default)
                adminUser.DateOfBirth = new DateTime(1990, 1, 1);
            if (string.IsNullOrEmpty(adminUser.ProfilePictureUrl))
                adminUser.ProfilePictureUrl = "/images/default-profile.png";
            if (adminUser.CreatedAt == default)
                adminUser.CreatedAt = DateTime.UtcNow;

            var updateResult = await userManager.UpdateAsync(adminUser);
            if (updateResult.Succeeded)
            {
                // Ensure admin role
                if (!await userManager.IsInRoleAsync(adminUser, "SystemAdmin"))
                {
                    await userManager.AddToRoleAsync(adminUser, "SystemAdmin");
                }
                await dbContext.SaveChangesAsync(); // Ensure changes are saved
                Console.WriteLine("Admin user updated successfully!");
            }
            else
            {
                var errors = string.Join("\n", updateResult.Errors.Select(e => e.Description));
                Console.WriteLine($"Failed to update admin user. Errors:\n{errors}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error during admin user creation/update: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
    }
}

// ===== Middleware Pipeline =====
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "notification",
    pattern: "Notification/MarkAsReadAndRedirect/{id}",
    defaults: new { controller = "Notification", action = "MarkAsReadAndRedirect" });
app.MapRazorPages();

// ===== Map SignalR Hub (must be AFTER routing) =====
app.MapHub<NotificationHub>("/notificationHub");

app.Run();
