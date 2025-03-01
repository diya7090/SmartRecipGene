using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace SmartRecipGene.Controllers
{
    [Authorize(Roles = "Admin")] // ✅ Only Admins can access this controller
    public class AdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
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

    }
}
