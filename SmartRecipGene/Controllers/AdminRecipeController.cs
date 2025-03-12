
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartRecipGene.Data;
using SmartRecipGene.Models;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Options;

namespace SmartRecipGene.Controllers
{
    public class AdminRecipeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminRecipeController> _logger;
        private readonly HttpClient _httpClient;
        private readonly SpoonacularSettings _spoonacularSettings;

        public AdminRecipeController(
                            IOptions<SpoonacularSettings> spoonacularSettings,

            ApplicationDbContext context,
            ILogger<AdminRecipeController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _spoonacularSettings = spoonacularSettings.Value;

        }

        public async Task<IActionResult> Index()
        {
            var recipes = await _context.Recipes.ToListAsync();
            return View(recipes);
        }

        [HttpGet]
        public async Task<IActionResult> GetRecipeSuggestions(string query)
        {
            if (string.IsNullOrEmpty(query))
                return Json(new string[] { });

            var suggestions = new HashSet<string>();

            var dbSuggestions = await _context.Recipes
                .Where(r => r.Title.Contains(query) ||
                           r.CusineType.Contains(query) ||
                           r.DietType.Contains(query))
                .Select(r => r.Title)
                .Take(5)
                .ToListAsync();

            foreach (var suggestion in dbSuggestions)
            {
                suggestions.Add(suggestion);
            }

            try
            {
                var apiKey = _spoonacularSettings.ApiKey;

               // string apiKey = "4f23d090497a4cc6942f7f6e1f3ffca4";
                string url = $"https://api.spoonacular.com/recipes/autocomplete?apiKey={apiKey}&query={query}&number=5";

                var response = await _httpClient.GetStringAsync(url);
                var apiSuggestions = JArray.Parse(response);

                foreach (var suggestion in apiSuggestions)
                {
                    suggestions.Add(suggestion["title"].ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"API Error: {ex.Message}");
            }

            return Json(suggestions.Take(5));
        }

        public async Task<IActionResult> Details(int id)
        {
            var recipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == id);
            if (recipe == null)
            {
                return NotFound();
            }
            return View(recipe);
        }
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Ingredients,Instructions,CookingTime,PreparationTime,Servings,ServingSize,DifficultyLevel,CusineType,DietType,Allergens,MealType,Equipment")] Recipe recipe)
        {
            if (ModelState.IsValid)
            {
                _context.Add(recipe);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(recipe);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe == null) return NotFound();

            return View(recipe);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ImageUrl,Title,Ingredients,Instructions,CookingTime,PreparationTime,Servings,ServingSize,DifficultyLevel,CusineType,DietType,Allergens,MealType,Equipment")] Recipe recipe)
        {
            if (id != recipe.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(recipe);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RecipeExists(recipe.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(recipe);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe == null) return NotFound();

            return View(recipe);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe != null)
            {
                _context.Recipes.Remove(recipe);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool RecipeExists(int id)
        {
            return _context.Recipes.Any(e => e.Id == id);
        }
    }
}