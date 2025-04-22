using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SmartRecipGene.Data;
using SmartRecipGene.Models;
using SmartRecipGene.Services;
using System.Linq;
using System.Threading.Tasks;

namespace SmartRecipGene.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly ISpoonacularService _spoonacularService; // ✅ Use the interface type

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context, ISpoonacularService spoonacularService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _spoonacularService = spoonacularService; // ✅ Corrected

        }

        // 🟢 1. List all users
        public async Task<IActionResult> Index()
        {
            var users = _userManager.Users.ToList();
            return View(users);
        }
        public async Task<IActionResult> OrderManagement()
        {
            var orders = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            return View(orders);
        }

        // POST: Admin/UpdateOrderStatus
        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return NotFound();
            }
            order.Status = status;
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
            return RedirectToAction("OrderManagement");
        }

        // GET: Admin/OrderDetails/5
        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            // Optionally include user info if needed
            // var user = await _context.Users.FindAsync(order.UserId);

            return View(order);
        }


        public async Task<IActionResult> Dashboard()
        {
            var totalUsers = await _userManager.Users.CountAsync();
            var totalDatabaseRecipes = await _context.Recipes.CountAsync();
            var totalRecipes = totalDatabaseRecipes; // 👈 Ignore API count

            var topRatedDbRecipes = await _context.Recipes
                .OrderByDescending(r => r.Reviews.Average(rv => rv.Rating))
                .Take(5)
                .Select(r => new { r.Title, AvgRating = r.Reviews.Average(rv => rv.Rating), SourceType = "database" })
                .ToListAsync();

            var topRatedApiRecipes = await _spoonacularService.GetTopRatedRecipes();

            var allTopRatedRecipes = topRatedDbRecipes
                .Concat(topRatedApiRecipes.Select(r => new { Title = r["title"].ToString(), AvgRating = 5.0, SourceType = "api" }))
                .OrderByDescending(r => r.AvgRating)
                .Take(10)
                .ToList();

            var shoppingListUsers = await _context.UserActivities
                .Where(a => a.ActivityType == "Shopping List Added")
                .Select(a => a.UserId)
                .Distinct()
                .CountAsync();

            var dashboardData = new
            {
                TotalUsers = totalUsers,
                TotalRecipes = totalRecipes, // ✅ Only using DB count
                TopRatedRecipes = allTopRatedRecipes,
                ShoppingListUsers = shoppingListUsers
            };

            return View(dashboardData);
        }







        // 🟢 2. Approve user accounts
        public async Task<IActionResult> Approve(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.EmailConfirmed = true; // ✅ Mark the email as confirmed
                await _userManager.UpdateAsync(user);
            }
            return RedirectToAction("Index");
        }

        // 🟢 3. Suspend/Ban user accounts
        public async Task<IActionResult> Suspend(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.LockoutEnd = DateTime.UtcNow.AddYears(100); // ✅ Suspend user indefinitely
                await _userManager.UpdateAsync(user);
            }
            return RedirectToAction("Index");
        }

        // 🟢 4. Assign a role to a user
        public async Task<IActionResult> AssignRole(string id, string role)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role)); // ✅ Create role if it doesn't exist
                }
                await _userManager.AddToRoleAsync(user, role);
            }
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ContactMessages()
        {
            var messages = await _context.Contacts
                .OrderByDescending(c => c.DateSubmitted)
                .ToListAsync();
            return View(messages);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleMessageRead(int id)
        {
            var message = await _context.Contacts.FindAsync(id);
            if (message != null)
            {
                message.IsRead = !message.IsRead;
                await _context.SaveChangesAsync();
                return Json(new { success = true, isRead = message.IsRead });
            }
            return Json(new { success = false });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var message = await _context.Contacts.FindAsync(id);
            if (message != null)
            {
                _context.Contacts.Remove(message);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }
    }

}