using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartRecipGene.Data;
using SmartRecipGene.Models;
using System.Security.Claims;

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
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<FavoriteRecipesController> _logger;

        public FavoriteRecipesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
            //  _logger = logger;
        }

        //public async Task<IActionResult> AddToFavorites(int recipeId, string title, string imageUrl)
        //{
        //    if (recipeId <= 0)
        //        return BadRequest("Valid Recipe ID is required");

        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        //    // Check if recipe exists in Recipes table
        //    var recipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == recipeId);

        //    if (recipe == null)
        //    {
        //        // If recipe doesn't exist, add it first
        //        recipe = new Recipe
        //        {
        //            Id = recipeId,
        //            Title = title,
        //            ImageUrl = imageUrl
        //        };
        //        _context.Recipes.Add(recipe);
        //        await _context.SaveChangesAsync();
        //    }

        //    // Check if already in favorites
        //    var existingFavorite = await _context.FavoriteRecipes
        //        .FirstOrDefaultAsync(f => f.UserId == userId && f.RecipeId == recipeId);

        //    if (existingFavorite == null)
        //    {
        //        var favoriteRecipe = new FavoriteRecipe
        //        {
        //            UserId = userId,
        //            RecipeId = recipeId,
        //            Title = title,
        //            ImageUrl = imageUrl,
        //            DateAdded = DateTime.UtcNow
        //        };
        //        _context.FavoriteRecipes.Add(favoriteRecipe);
        //        await _context.SaveChangesAsync();
        //        TempData["Success"] = "Recipe added to favorites!";
        //    }
        //    else
        //    {
        //        TempData["Info"] = "Recipe is already in favorites.";
        //    }

        //    return RedirectToAction("Index", "Home");
        //}

        //------
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> AddToFavorites(int? recipeId, string title, string imageUrl)
        //{
        //    if (recipeId == null)
        //    { 
        //        return NotFound();
        //    }

        //    var userId = _userManager.GetUserId(User);
        //    if (userId == null)
        //    {
        //        return Challenge();
        //    }

        //    var existingFavorite = await _context.FavoriteRecipes
        //        .FirstOrDefaultAsync(f => f.RecipeId == recipeId && f.UserId == userId);

        //    if (existingFavorite != null)
        //    {
        //        return RedirectToAction(nameof(Index));
        //    }

        //    var favoriteRecipe = new FavoriteRecipe
        //    {
        //        RecipeId = recipeId,
        //        Title = title,
        //        ImageUrl = imageUrl,
        //        UserId = userId,
        //        DateAdded = DateTime.UtcNow
        //    };

        //    _context.Add(favoriteRecipe);
        //    await _context.SaveChangesAsync();

        //    return RedirectToAction(nameof(Index));
        //}

        public async Task<IActionResult> AddToFavorites(string recipeId, string title, string imageUrl)
{
    if (string.IsNullOrEmpty(recipeId))
        return BadRequest("Recipe ID is required");

    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

    // Check if already in favorites
    var existingFavorite = await _context.FavoriteRecipes
        .FirstOrDefaultAsync(f => f.UserId == userId && f.RecipeId.ToString() == recipeId);

    if (existingFavorite == null)
    {
        var favoriteRecipe = new FavoriteRecipe
        {
            UserId = userId,
            RecipeId = int.Parse(recipeId),
            Title = title,
            ImageUrl = imageUrl,
            DateAdded = DateTime.UtcNow
        };
        _context.FavoriteRecipes.Add(favoriteRecipe);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Recipe added to favorites!";
    }
    else
    {
        TempData["Info"] = "Recipe is already in favorites.";
    }

    return RedirectToAction("Index", "Home");
}
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var favorites = await _context.FavoriteRecipes
                .Where(f => f.UserId == user.Id)
                .ToListAsync();

            return View(favorites);
        }

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
        // View Favorite Recipes
        //    public async Task<IActionResult> Index()
        //    {
        //        var user = await _userManager.GetUserAsync(User);
        //        if (user == null) return RedirectToAction("Login", "Account");

        //        var favorites = await _context.FavoriteRecipes
        //            .Where(f => f.UserId == user.Id)
        //            .ToListAsync();

        //        return View(favorites);
        //    }

        //    // Remove from Favorites
        //    [HttpPost]
        //    public async Task<IActionResult> RemoveFromFavorites(int id)
        //    {
        //        var favorite = await _context.FavoriteRecipes.FindAsync(id);
        //        if (favorite != null)
        //        {
        //            _context.FavoriteRecipes.Remove(favorite);
        //            await _context.SaveChangesAsync();
        //        }

        //        return RedirectToAction("Index");
        //    }
        //}
    }

}