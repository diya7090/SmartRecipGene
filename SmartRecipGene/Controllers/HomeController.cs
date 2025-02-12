using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SmartRecipGene.Data;
using SmartRecipGene.Models;
using SmartRecipGene.Services;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Claims;

namespace SmartRecipGene.Controllers
{
    public class HomeController : Controller
    {

        private readonly SpoonacularService _spoonacularService;

        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient; // Declare as a private readonly field

        public HomeController(ApplicationDbContext context, SpoonacularService spoonacularService, IHttpClientFactory httpClientFactory )
        {
            _context = context ;
            _spoonacularService = spoonacularService;
            _httpClient = httpClientFactory.CreateClient(); // Correct way to initialize HttpClient
        }

        [HttpPost]
        public async Task<IActionResult> GetRecipes(string ingredients)
        {
            // Call the API with the entered ingredients
            var response = await _spoonacularService.GetRecipesByIngredientsAsync(ingredients);

            // Parse the JSON response
            var recipes = JArray.Parse(response);

            // Pass the recipes to the view
            return View("Recipes", recipes);
        }

        public IActionResult Index()
        {
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> RecipeDetails(int id)
        {
            var response = await _spoonacularService.GetRecipeDetailsAsync(id);
            var recipe = JObject.Parse(response);

            var reviews = _context.Reviews.Where(r => r.RecipeId == id).ToList();
            ViewBag.Reviews = reviews;

            return View(recipe);
        }
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SubmitReview(int RecipeId, int Rating, string Comment)
        {
            var review = new Review
            {
                RecipeId = RecipeId,
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Rating = Rating,
                Comment = Comment
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return RedirectToAction("RecipeDetails", new { id = RecipeId });
        }

        [HttpGet]
        public async Task<IActionResult> SearchRecipes(string query, string mealType, string diet)
        {
            string apiKey = "4f23d090497a4cc6942f7f6e1f3ffca4";
            string url = $"https://api.spoonacular.com/recipes/complexSearch?apiKey={apiKey}&query={query}&number=100";


            // Append Meal Type Filter
            if (!string.IsNullOrEmpty(mealType))
            {
                switch (mealType.ToLower())
                {
                    case "vegetarian":
                        url += $"&excludeIngredients=eggs,chicken,shrimp,duck,crabs,beef,pork,fish,mutton";
                        break;
                    case "vegan":
                    case "gluten-free":
                        url += $"&diet={mealType.ToLower()}";
                        break;

                    case "eggless":
                        url += "&diet=vegetarian&excludeIngredients=egg";  // Eggless is treated as Vegetarian minus eggs
                        break;

                    case "dessert":
                        url += "&type=dessert"; // 🍰 Fetch only desserts
                        break;

                    default:
                        break;
                }
            }

            // Append additional dietary filter if needed
            if (!string.IsNullOrEmpty(diet))
            {
                url += $"&diet={diet.ToLower()}";
            }
            if (!string.IsNullOrEmpty(mealType))
            {
                url += $"&cuisine={mealType.ToLower()}";
            }

            try
            {
                var response = await _httpClient.GetStringAsync(url);
                var jsonResponse = JObject.Parse(response);

                if (!jsonResponse.ContainsKey("results") || !jsonResponse["results"].HasValues)
                {
                    ViewBag.Message = "No recipes found based on your filters.";
                    return View("RecipeResults", new JArray()); // Return an empty array
                }

                var recipes = jsonResponse["results"];
                return View("RecipeResults", recipes);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine("API Error: " + ex.Message);
                ViewBag.Message = "Error fetching recipes. Please try again later.";
                return View("RecipeResults", new JArray());
            }
        }
        public IActionResult Ingredients()
        {
            return View();
        }
        public IActionResult Recipes()
        {
            // Example of getting a JArray (this could be fetched from an API or database)
            string json = "[{\"title\":\"Recipe 1\",\"usedIngredientCount\":3,\"missedIngredientCount\":2,\"id\":1}, {\"title\":\"Recipe 2\",\"usedIngredientCount\":4,\"missedIngredientCount\":1,\"id\":2}]";
            JArray recipes = JArray.Parse(json);

            return View(recipes); // Pass the JArray to the view
        }
        
        //public async Task<IActionResult> SearchRecipes(string query, string cuisine, string mealType, string diet)
        //{
        //    string apiKey = "4f23d090497a4cc6942f7f6e1f3ffca4";
        //    string url = $"https://api.spoonacular.com/recipes/complexSearch?apiKey={apiKey}";

        //    if (!string.IsNullOrEmpty(query))
        //        url += $"&query={query}";
        //    if (!string.IsNullOrEmpty(cuisine))
        //        url += $"&cuisine={cuisine}";
        //    if (!string.IsNullOrEmpty(mealType))
        //        url += $"&type={mealType}";
        //    if (!string.IsNullOrEmpty(diet))
        //        url += $"&diet={diet}";

        //    try
        //    {
        //        var response = await _httpClient.GetStringAsync(url);
        //        Console.WriteLine("Full API Response: " + response); // ✅ Log the full response

        //        var jsonResponse = JObject.Parse(response);

        //        if (!jsonResponse.ContainsKey("results") || !jsonResponse["results"].HasValues)
        //        {
        //            ViewBag.Message = "No recipes found based on your ingredients.";
        //            return View("Recipes", new JArray()); // Return an empty array
        //        }

        //        var recipes = jsonResponse["results"];
        //        return View("Recipes", recipes);
        //    }
        //    catch (HttpRequestException ex)
        //    {
        //        Console.WriteLine("API Error: " + ex.Message);
        //        ViewBag.Message = "Error fetching recipes. Please try again later.";
        //        return View("Recipes", new JArray());
        //    }
        //}
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddToShoppingList(string ingredient)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var shoppingItem = new ShoppingListItem
            {
                UserId = userId,
                Ingredient = ingredient
            };

            _context.ShoppingList.Add(shoppingItem);
            await _context.SaveChangesAsync();

            return RedirectToAction("ShoppingList");
        }


        [HttpGet]
        [Authorize]
        public IActionResult ShoppingList()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var items = _context.ShoppingList.Where(x => x.UserId == userId).ToList();

            return View(items);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> MarkAsPurchased(int id)
        {
            var item = await _context.ShoppingList.FindAsync(id);
            if (item != null)
            {
                item.Purchased = true;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("ShoppingList");
        }


        [HttpPost]
        [Authorize]
        public async Task<IActionResult> RemoveFromShoppingList(int id)
        {
            var item = await _context.ShoppingList.FindAsync(id);
            if (item != null)  // ✅ Add null check to avoid CS8602
            {
                _context.ShoppingList.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("ShoppingList");
        }





        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
