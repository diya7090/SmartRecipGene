using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartRecipGene.Data;
using SmartRecipGene.Models;
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

        public AdminController(
            UserManager<ApplicationUser> userManager, 
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // 🟢 1. List all users
        public async Task<IActionResult> Index()
        {
            var users = _userManager.Users.ToList();
            return View(users);
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
