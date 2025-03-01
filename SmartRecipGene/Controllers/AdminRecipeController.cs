using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartRecipGene.Data;
using SmartRecipGene.Models;
using System.Threading.Tasks;
using System.Linq;

namespace SmartRecipGene.Controllers
{
   // [Authorize(Roles = "Admin")] // Only Admins can access

    public class AdminRecipeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminRecipeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1️⃣ GET: Admin/Recipe (List all recipes)
        public async Task<IActionResult> Index()
        {
            var recipes = await _context.Recipes.ToListAsync();
            return View(recipes);
        }

        // 2️⃣ GET: Admin/Recipe/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe == null) return NotFound();

            return View(recipe);
        }

        // 3️⃣ GET: Admin/Recipe/Create (Show Form)
        public IActionResult Create()
        {
            return View();
        }

        // 4️⃣ POST: Admin/Recipe/Create (Save Data)
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

        // 5️⃣ GET: Admin/Recipe/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe == null) return NotFound();

            return View(recipe);
        }

        // 6️⃣ POST: Admin/Recipe/Edit/5 (Update)
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

        // 7️⃣ GET: Admin/Recipe/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe == null) return NotFound();

            return View(recipe);
        }

        // 8️⃣ POST: Admin/Recipe/Delete/5 (Confirm Delete)
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
