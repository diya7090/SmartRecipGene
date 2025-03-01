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
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json; // Add this for session access


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

        public async Task<IActionResult> GetRecipes(string ingredients)
        {
            var response = await _spoonacularService.GetRecipesByIngredientsAsync(ingredients);
            var recipes = JArray.Parse(response);

            // Store data in session instead of TempData
            HttpContext.Session.SetString("Recipes", recipes.ToString(Newtonsoft.Json.Formatting.None));

            return RedirectToAction("RecipeResults");
        }

        public IActionResult RecipeResults()
        {
            var recipesJson = HttpContext.Session.GetString("Recipes");

            if (string.IsNullOrEmpty(recipesJson))
            {
                return RedirectToAction("Index"); // Redirect if session is empty
            }

            var recipes = JsonConvert.DeserializeObject<JArray>(recipesJson);
            return View("Recipes", recipes);
        }

        public IActionResult Index()
        {
            return View();
        }



        [HttpGet]
        public async Task<IActionResult> RecipeDetails(int id)
        {
            JObject recipeData;
            List<Review> reviews = new List<Review>();

            try
            {
                // Get reviews from database
                reviews = await _context.Reviews.Where(r => r.RecipeId == id).ToListAsync();

                // First try to get recipe from database
                var dbRecipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == id);

                if (dbRecipe != null)
                {
                    // Create recipe data from database
                    recipeData = new JObject
                    {
                        ["id"] = dbRecipe.Id,
                        ["title"] = dbRecipe.Title ?? "No Title Available",
                        ["extendedIngredients"] = !string.IsNullOrEmpty(dbRecipe.Ingredients)
                            ? JArray.Parse("[" + string.Join(",", dbRecipe.Ingredients.Split(',').Select(i => $"{{\"original\":\"{i.Trim()}\"}}")) + "]")
                            : new JArray(),
                        ["instructions"] = dbRecipe.Instructions ?? "No instructions available.",
                        ["readyInMinutes"] = dbRecipe.CookingTime,
                        ["servings"] = dbRecipe.Servings,
                        ["image"] = dbRecipe.ImageUrl ?? "",
                        ["sourceType"] = "database"
                    };
                }
                else
                {
                    // If not in database, try API
                    string apiUrl = $"https://api.spoonacular.com/recipes/{id}/information?apiKey=4f23d090497a4cc6942f7f6e1f3ffca4";
                    var response = await _httpClient.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var apiRecipe = JObject.Parse(content);

                        recipeData = new JObject
                        {
                            ["id"] = id,
                            ["title"] = apiRecipe["title"]?.ToString() ?? "No Title Available",
                            ["extendedIngredients"] = apiRecipe["extendedIngredients"] ?? new JArray(),
                            ["instructions"] = apiRecipe["instructions"]?.ToString() ?? "No instructions available.",
                            ["readyInMinutes"] = apiRecipe["readyInMinutes"]?.ToString() ?? "Unknown",
                            ["servings"] = apiRecipe["servings"]?.ToString() ?? "Unknown",
                            ["image"] = apiRecipe["image"]?.ToString() ?? "",
                            ["sourceType"] = "api"
                        };

                        // Optional: Save API recipe to database for future use
                       // await SaveRecipeToDatabase(recipeData);
                    }
                    else
                    {
                        throw new Exception("Recipe not found in API");
                    }
                }

                ViewBag.Reviews = reviews;
                return View(recipeData);
            }
            catch (Exception ex)
            {
                // Fallback recipe data
                recipeData = new JObject
                {
                    ["id"] = id,
                    ["title"] = "Recipe Details Currently Unavailable",
                    ["extendedIngredients"] = new JArray(),
                    ["instructions"] = "Unable to load recipe details. Please try again later.",
                    ["readyInMinutes"] = "Unknown",
                    ["servings"] = "Unknown",
                   ["image"] = "",
                    ["sourceType"] = "unknown"
                };

                ViewBag.Reviews = reviews;
                ViewBag.Error = "An error occurred while loading the recipe.";
                return View(recipeData);
            }
        }

        private bool ContainsNonVegetarianIngredients(JToken recipe)
        {
            var ingredients = recipe["extendedIngredients"]?.ToObject<JArray>();
            if (ingredients == null) return false;

            var nonVegKeywords = new[] { "egg", "eggs", "chicken", "beef", "pork", "fish", "mutton",
        "shrimp", "crab", "duck", "meat", "bacon", "ham", "turkey", "seafood", "prawn", "mayonnaise" };

            return ingredients.Any(ingredient =>
                nonVegKeywords.Any(keyword =>
                    ingredient["name"]?.ToString().ToLower().Contains(keyword) == true));
        }

        [HttpGet]
        public async Task<IActionResult> SearchRecipes(string query, string cuisine, string mealType, string diet)
        {
            var combinedResults = new JArray();

            // First, search in database
            var dbQuery = _context.Recipes.AsQueryable();

            if (!string.IsNullOrEmpty(query))
            {
                dbQuery = dbQuery.Where(r => r.Title.Contains(query));
            }
            if (!string.IsNullOrEmpty(cuisine))
            {
                dbQuery = dbQuery.Where(r => r.CusineType == cuisine);
            }
            if (!string.IsNullOrEmpty(mealType))
            {
                dbQuery = dbQuery.Where(r => r.MealType == mealType);
            }

            var dbRecipes = await dbQuery.ToListAsync();
            foreach (var recipe in dbRecipes)
            {
                var recipeJson = new JObject
                {
                    ["id"] = recipe.Id,
                    ["title"] = recipe.Title,
                    ["image"] = recipe.ImageUrl ?? "",
                    ["readyInMinutes"] = recipe.CookingTime,
                    ["servings"] = recipe.Servings,
                    ["sourceType"] = "database"
                };
                combinedResults.Add(recipeJson);
            }

            // Then search in API
            string apiKey = "4f23d090497a4cc6942f7f6e1f3ffca4";
            string url = $"https://api.spoonacular.com/recipes/complexSearch?apiKey={apiKey}&query={query}&number=100";

            if (!string.IsNullOrEmpty(cuisine))
            {
                url += $"&cuisine={cuisine.ToLower()}";
            }
            if (!string.IsNullOrEmpty(mealType))
            {
                url += $"&type={mealType.ToLower()}";
            }
            if (!string.IsNullOrEmpty(diet))
            {
                switch (diet.ToLower())
                {
                    case "vegetarian":
                        url += "&diet=vegetarian";
                        url += "&excludeIngredients=chicken,beef,pork,fish,mutton,shrimp,crab,duck,meat,bacon,ham,turkey,seafood,prawn,egg,eggs,mayonnaise";
                        break;
                    case "vegan":
                        url += "&diet=vegan";
                        break;
                    case "gluten-free":
                        url += "&diet=gluten-free";
                        break;
                }
            }

            url += "&fillIngredients=true&addRecipeInformation=true";

            try
            {
                var response = await _httpClient.GetStringAsync(url);
                var jsonResponse = JObject.Parse(response);

                if (jsonResponse.ContainsKey("results") && jsonResponse["results"].HasValues)
                {
                    var apiRecipes = jsonResponse["results"].ToObject<JArray>();
                    foreach (var recipe in apiRecipes)
                    {
                        recipe["sourceType"] = "api";
                        combinedResults.Add(recipe);
                    }
                }

                // Apply vegetarian filtering if needed
                if (diet?.ToLower() == "vegetarian")
                {
                    combinedResults = new JArray(
                        combinedResults.Where(r =>
                            (r["sourceType"].ToString() == "database") ||
                            (r["sourceType"].ToString() == "api" &&
                            r["vegetarian"]?.Value<bool>() == true &&
                            !ContainsNonVegetarianIngredients(r) &&
                            !r["title"].ToString().ToLower().Contains("egg"))
                        )
                    );
                }

                if (!combinedResults.HasValues)
                {
                    ViewBag.Message = "No recipes found based on your filters.";
                    return View("RecipeResults", new JArray());
                }

                return View("RecipeResults", combinedResults);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"API Error: {ex.Message}");
                ViewBag.Message = "Error fetching recipes from API. Showing database results only.";
                return View("RecipeResults", combinedResults);
            }
        }
        //[HttpGet]
        //public async Task<IActionResult> SearchRecipes(string query, string cuisine, string mealType, string diet)
        //{
        //    string apiKey = "4f23d090497a4cc6942f7f6e1f3ffca4";
        //    string url = $"https://api.spoonacular.com/recipes/complexSearch?apiKey={apiKey}&query={query}&number=100";

        //    // Add cuisine filter if specified
        //    if (!string.IsNullOrEmpty(cuisine))
        //    {
        //        url += $"&cuisine={cuisine.ToLower()}";
        //    }

        //    // Add meal type filter if specified
        //    if (!string.IsNullOrEmpty(mealType))
        //    {
        //        url += $"&type={mealType.ToLower()}";
        //    }

        //    // Handle dietary restrictions
        //    if (!string.IsNullOrEmpty(diet))
        //    {
        //        switch (diet.ToLower())
        //        {
        //            case "vegetarian":
        //                url += "&diet=vegetarian";
        //                url += "&excludeIngredients=chicken,beef,pork,fish,mutton,shrimp,crab,duck,meat,bacon,ham,turkey,seafood,prawn,egg,eggs,mayonnaise";
        //                url += "&instructionsRequired=true";
        //                break;
        //            case "vegan":
        //                url += "&diet=vegan";
        //                break;
        //            case "gluten-free":
        //                url += "&diet=gluten-free";
        //                break;
        //        }
        //    }

        //    // Add additional parameters
        //    url += "&fillIngredients=true&addRecipeInformation=true";

        //    try
        //    {
        //        var response = await _httpClient.GetStringAsync(url);
        //        var jsonResponse = JObject.Parse(response);

        //        if (!jsonResponse.ContainsKey("results") || !jsonResponse["results"].HasValues)
        //        {
        //            ViewBag.Message = "No recipes found based on your filters.";
        //            return View("RecipeResults", new JArray());
        //        }

        //        var recipes = jsonResponse["results"].ToObject<JArray>();

        //        // Apply additional filtering for vegetarian recipes if needed
        //        if (diet?.ToLower() == "vegetarian")
        //        {
        //            recipes = new JArray(
        //                recipes.Where(r =>
        //                    r["vegetarian"]?.Value<bool>() == true &&
        //                    !ContainsNonVegetarianIngredients(r) &&
        //                    !r["title"].ToString().ToLower().Contains("egg")
        //                )
        //            );
        //        }

        //        if (!recipes.HasValues)
        //        {
        //            ViewBag.Message = "No recipes found based on your filters.";
        //            return View("RecipeResults", new JArray());
        //        }

        //        return View("RecipeResults", recipes);
        //    }
        //    catch (HttpRequestException ex)
        //    {
        //        Console.WriteLine($"API Error: {ex.Message}");
        //        ViewBag.Message = "Error fetching recipes. Please try again later.";
        //        return View("RecipeResults", new JArray());
        //    }
        //}

        [Authorize]

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
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AllReviews()
        {
            var reviews = await _context.Reviews
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            var recipeDetails = new Dictionary<int, string>();

            foreach (var review in reviews)
            {
                try
                {
                    if (!recipeDetails.ContainsKey(review.RecipeId))  // Only fetch if not already in dictionary
                    {
                        using (var httpClient = new HttpClient())
                        {
                            string apiUrl = $"https://api.spoonacular.com/recipes/{review.RecipeId}/information?apiKey=4f23d090497a4cc6942f7f6e1f3ffca4";
                            var response = await httpClient.GetAsync(apiUrl);

                            if (response.IsSuccessStatusCode)
                            {
                                string jsonResponse = await response.Content.ReadAsStringAsync();
                                var recipeData = JObject.Parse(jsonResponse);
                                recipeDetails[review.RecipeId] = recipeData["title"]?.ToString() ?? $"Recipe {review.RecipeId}";
                            }
                            else
                            {
                                recipeDetails[review.RecipeId] = $"Recipe {review.RecipeId}";
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    recipeDetails[review.RecipeId] = $"Recipe {review.RecipeId}";
                }
            }

            ViewBag.RecipeDetails = recipeDetails;
            return View("~/Views/Admin/AllReviews.cshtml", reviews);
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
