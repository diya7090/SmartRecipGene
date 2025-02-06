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
    [Authorize]

    //public class FavoriteRecipesController : Controller
    //{
    //    public IActionResult Index()
    //    {
    //        return View();
    //    }
    //}
    public class FavoriteRecipesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public FavoriteRecipesController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Add to Favorites
        [HttpPost]
        public async Task<IActionResult> AddToFavorites(string recipeId, string title, string imageUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var existingFavorite = await _context.FavoriteRecipes
                .FirstOrDefaultAsync(f => f.RecipeId == recipeId && f.UserId == user.Id);

            if (existingFavorite == null)
            {
                var favorite = new FavoriteRecipe
                {
                    RecipeId = recipeId,
                    Title = title,
                    ImageUrl = imageUrl,
                    UserId = user.Id
                };

                _context.FavoriteRecipes.Add(favorite);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        // View Favorite Recipes
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var favorites = await _context.FavoriteRecipes
                .Where(f => f.UserId == user.Id)
                .ToListAsync();

            return View(favorites);
        }

        // Remove from Favorites
        [HttpPost]
        public async Task<IActionResult> RemoveFromFavorites(int id)
        {
            var favorite = await _context.FavoriteRecipes.FindAsync(id);
            if (favorite != null)
            {
                _context.FavoriteRecipes.Remove(favorite);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }
    }
}
