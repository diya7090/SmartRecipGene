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

            // Fetch user names for all user IDs in the orders
            var userIds = orders.Select(o => o.UserId).Distinct().ToList();
            var users = await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserName);

            ViewBag.UserNames = users;

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

        // ... existing code ...

        public async Task<IActionResult> Dashboard()
        {
             // Example: Kits Sold per Recipe (for KitsSoldChart)
    var kitsSold = await _context.Recipes
        .Select(r => new
        {
            Title = r.Title,
            Sold = _context.OrderItems.Where(oi => oi.RecipeId == r.Id).Sum(oi => (int?)oi.Servings) ?? 0
        })
        .ToListAsync();

    var kitsSoldLabels = kitsSold.Select(k => k.Title).ToList();
    var kitsSoldData = kitsSold.Select(k => k.Sold).ToList();

    // Example: Top Rated Recipes (for TopRatedChart)
    var topRated = await _context.Recipes
        .Where(r => r.Reviews.Any())
        .OrderByDescending(r => r.Reviews.Average(rv => rv.Rating))
        .Take(5)
        .Select(r => new
        {
            Title = r.Title,
            AvgRating = r.Reviews.Average(rv => rv.Rating)
        })
        .ToListAsync();

    var topRatedLabels = topRated.Select(t => t.Title).ToList();
    var topRatedData = topRated.Select(t => t.AvgRating).ToList();

    // Example: Most Saved Recipes (for MostSavedChart)
    var mostSaved = await _context.Recipes
        .Select(r => new
        {
            Title = r.Title,
            Saves = _context.FavoriteRecipes.Count(rs => rs.RecipeId == r.Id)
        })
        .OrderByDescending(r => r.Saves)
        .Take(5)
        .ToListAsync();

    var mostSavedLabels = mostSaved.Select(m => m.Title).ToList();
    var mostSavedData = mostSaved.Select(m => m.Saves).ToList();
            var totalUsers = await _userManager.Users.CountAsync();
            var totalDatabaseRecipes = await _context.Recipes.CountAsync();
            var totalRecipes = totalDatabaseRecipes;

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

            // Calculate revenue and sales from orders
            var pastOrders = await _context.Orders.ToListAsync();
            decimal totalRevenue = 0.0m;
            int totalSales = 0;
            int totalUsersPurchased = 0;
            int ordersShipped = 0, ordersProcessing = 0, ordersPending = 0, ordersDelivered = 0;

            if (pastOrders != null)
            {
                totalRevenue = pastOrders.Sum(o => o.TotalAmount);
                totalSales = pastOrders.Count();
                totalUsersPurchased = pastOrders.Select(o => o.UserId).Distinct().Count();
                ordersShipped = pastOrders.Count(o => o.Status == "Shipped");
                ordersProcessing = pastOrders.Count(o => o.Status == "Processing");
                ordersPending = pastOrders.Count(o => o.Status == "Pending");
                ordersDelivered = pastOrders.Count(o => o.Status == "Delivered");
            }

            // Fetch all recipes for the dashboard table
            var allRecipes = await _context.Recipes
                .Select(r => new
                {
                    Title = r.Title,
                    // If you have a category property, use it here. Otherwise, remove or replace.
                     Category = r.CusineType,
                    AvgRating = r.Reviews.Any() ? r.Reviews.Average(rv => rv.Rating) : 0,
                    TimesOrdered = _context.OrderItems.Count(oi => oi.RecipeId == r.Id)
                })
                .ToListAsync();

            // Fetch all users for the dashboard table
            var allUsers = await _userManager.Users
                .Select(u => new
                {
                    Name = u.UserName,
                    Email = u.Email,
                    OrdersPlaced = _context.Orders.Count(o => o.UserId == u.Id)
                    // LastActive removed since it does not exist
                })
                .ToListAsync();

            var dashboardData = new
            {
                TotalUsers = totalUsers,
                TotalRecipes = totalRecipes,
                TopRatedRecipes = allTopRatedRecipes,
                ShoppingListUsers = shoppingListUsers,
                PastOrders = pastOrders,
                TotalRevenue = totalRevenue,
                TotalSales = totalSales,
                TotalUsersPurchased = totalUsersPurchased,
                OrdersShipped = ordersShipped,
                OrdersProcessing = ordersProcessing,
                OrdersPending = ordersPending,
                OrdersDelivered = ordersDelivered,
                AllRecipes = allRecipes,
                AllUsers = allUsers,
                KitsSoldLabels = kitsSoldLabels,
        KitsSoldData = kitsSoldData,
        TopRatedLabels = topRatedLabels,
        TopRatedData = topRatedData,
        MostSavedLabels = mostSavedLabels,
        MostSavedData = mostSavedData
            };

            return View(dashboardData);
        }        // ... existing code ...        //public async Task<IActionResult> Dashboard()
        //{
        //    var totalUsers = await _userManager.Users.CountAsync();
        //    var totalDatabaseRecipes = await _context.Recipes.CountAsync();
        //    var totalRecipes = totalDatabaseRecipes; // 👈 Ignore API count

        //    var topRatedDbRecipes = await _context.Recipes
        //        .OrderByDescending(r => r.Reviews.Average(rv => rv.Rating))
        //        .Take(5)
        //        .Select(r => new { r.Title, AvgRating = r.Reviews.Average(rv => rv.Rating), SourceType = "database" })
        //        .ToListAsync();

        //    var topRatedApiRecipes = await _spoonacularService.GetTopRatedRecipes();

        //    var allTopRatedRecipes = topRatedDbRecipes
        //        .Concat(topRatedApiRecipes.Select(r => new { Title = r["title"].ToString(), AvgRating = 5.0, SourceType = "api" }))
        //        .OrderByDescending(r => r.AvgRating)
        //        .Take(10)
        //        .ToList();

        //    var shoppingListUsers = await _context.UserActivities
        //        .Where(a => a.ActivityType == "Shopping List Added")
        //        .Select(a => a.UserId)
        //        .Distinct()
        //        .CountAsync();

        //    var dashboardData = new
        //    {
        //        TotalUsers = totalUsers,
        //        TotalRecipes = totalRecipes, // ✅ Only using DB count
        //        TopRatedRecipes = allTopRatedRecipes,
        //        ShoppingListUsers = shoppingListUsers
        //    };

        //    return View(dashboardData);
        //}







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