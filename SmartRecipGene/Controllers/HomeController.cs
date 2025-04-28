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
using Newtonsoft.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options; // Add this for session access
using System.Linq;

namespace SmartRecipGene.Controllers
{
    public class HomeController : Controller
    {
        private readonly SpoonacularSettings _spoonacularSettings;
            private readonly SpoonacularService _spoonacularService;
            private readonly ApplicationDbContext _context;
            private readonly HttpClient _httpClient;
            private readonly ILogger<HomeController> _logger;

        public HomeController(
            IOptions<SpoonacularSettings> spoonacularSettings,
            ApplicationDbContext context,
            SpoonacularService spoonacularService,
            IHttpClientFactory httpClientFactory,
            ILogger<HomeController> logger)
        {
            _context = context;
            _spoonacularService = spoonacularService;
            _spoonacularSettings = spoonacularSettings.Value;

                _httpClient = httpClientFactory.CreateClient();
                _logger = logger;
            }
            public async Task<IActionResult> GetRecipes(string ingredients, int page = 1)
{
    const int PageSize = 12;
    var combinedResults = new List<JToken>();

            

            if (!string.IsNullOrEmpty(ingredients))
            {
                // Database search
                var ingredientList = ingredients.Split(',').Select(i => i.Trim().ToLower()).ToList();
                var dbRecipes = await _context.Recipes
                    .Where(r => r.Ingredients != null &&
                        ingredientList.Any(i => r.Ingredients.ToLower().Contains(i)))
                    .ToListAsync();

        foreach (var recipe in dbRecipes)
        {
            var usedIngredients = ingredientList.Count(i =>
                recipe.Ingredients.ToLower().Contains(i.ToLower()));

            var recipeJson = new JObject
            {
                ["id"] = recipe.Id,
                ["title"] = recipe.Title,
                ["image"] = recipe.ImageUrl ?? "",
                ["sourceType"] = "database",
                ["readyInMinutes"] = recipe.CookingTime,
                ["servings"] = recipe.Servings,
                ["usedIngredientCount"] = usedIngredients,
                ["missedIngredientCount"] = ingredientList.Count - usedIngredients,
                ["likes"] = 0,
                ["vegetarian"] = recipe.DietType?.ToLower() == "vegetarian",
                
            };
            combinedResults.Add(recipeJson);
        }

        // API search
        try
        {
            var baseUrl = "https://api.spoonacular.com/recipes/findByIngredients";
            var queryParams = new Dictionary<string, string>
            {
                ["apiKey"] = _spoonacularSettings.ApiKey,
                ["ingredients"] = ingredients,
                ["number"] = "20",
                ["ranking"] = "2",
                ["ignorePantry"] = "true"
            };

            string url = baseUrl + "?" + string.Join("&", queryParams.Select(p =>
                $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

            var response = await _httpClient.GetStringAsync(url);
            var apiRecipes = JArray.Parse(response);

            foreach (var recipe in apiRecipes)
            {
                recipe["sourceType"] = "api";
            
                combinedResults.Add(recipe);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching recipes from API");
            ViewBag.Message = "Error fetching recipes from API. Showing database results only.";
        }

        if (!combinedResults.Any())
        {
            ViewBag.Message = "No recipes found with these ingredients.";
            return View("RecipeResults", PaginatedList<JToken>.Create(new List<JToken>(), page, PageSize));
        }
    }

    // Apply pagination to combined results
    var paginatedResults = PaginatedList<JToken>.Create(combinedResults, page, PageSize);
    HttpContext.Session.SetString("Recipes", JsonConvert.SerializeObject(combinedResults));
    HttpContext.Session.SetInt32("CurrentPage", page);
    
    ViewBag.Ingredients = ingredients;
    return View("RecipeResults", paginatedResults);
}
       
        public async Task<IActionResult> RecipeResults()
        {
            var recipesJson = HttpContext.Session.GetString("Recipes");

            if (string.IsNullOrEmpty(recipesJson))
            {
                return RedirectToAction("Index");
            }

            var combinedResults = JArray.Parse(recipesJson);
            return View("Recipes", combinedResults);
        }
       
        public async Task<IActionResult> Index()
        {
            try
            {
                // Get most loved recipes based on review count
                var mostLovedRecipes = await _context.Recipes
                    .OrderByDescending(r => _context.Reviews.Count(rev => rev.RecipeId == r.Id))
                    .Take(6)
                    .Select(r => new
                    {
                        Id = r.Id,
                        Title = r.Title,
                        ImageUrl = r.ImageUrl,
                        ReviewCount = _context.Reviews.Count(rev => rev.RecipeId == r.Id)
                    })
                    .ToListAsync();

                // If not enough recipes in database, fetch from API
                if (mostLovedRecipes.Count < 6)
                {
                    var apiKey = _spoonacularSettings.ApiKey;
                    string url = $"https://api.spoonacular.com/recipes/random?apiKey={apiKey}&number={6 - mostLovedRecipes.Count}";
                    
                    var response = await _httpClient.GetStringAsync(url);
                    var apiResponse = JObject.Parse(response);
                    
                    if (apiResponse["recipes"] != null)
                    {
                        var apiRecipes = apiResponse["recipes"].Select(r => new
                        {
                            Id = (int)r["id"],
                            Title = (string)r["title"],
                            ImageUrl = (string)r["image"],
                            ReviewCount = 0
                        });
                        
                        mostLovedRecipes.AddRange(apiRecipes);
                    }
                }

                // Get recommended recipes based on ID (since CreatedDate is not available)
                var recommendedRecipes = await _context.Recipes
                    .OrderByDescending(r => r.Id) // Using Id instead of CreatedDate
                    .Take(6)
                    .Select(r => new
                    {
                        Id = r.Id,
                        Title = r.Title,
                        ImageUrl = r.ImageUrl
                    })
                    .ToListAsync();

                // If not enough recommended recipes, fetch from API
                if (recommendedRecipes.Count < 6)
                {
                    var apiKey = _spoonacularSettings.ApiKey;
                    string url = $"https://api.spoonacular.com/recipes/random?apiKey={apiKey}&number={6 - recommendedRecipes.Count}";
                    
                    var response = await _httpClient.GetStringAsync(url);
                    var apiResponse = JObject.Parse(response);
                    
                    if (apiResponse["recipes"] != null)
                    {
                        var apiRecipes = apiResponse["recipes"].Select(r => new
                        {
                            Id = (int)r["id"],
                            Title = (string)r["title"],
                            ImageUrl = (string)r["image"]
                        });
                        
                        recommendedRecipes.AddRange(apiRecipes);
                    }
                }

                ViewBag.MostLovedRecipes = mostLovedRecipes;
                ViewBag.RecommendedRecipes = recommendedRecipes;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading home page recipes");
                ViewBag.MostLovedRecipes = new List<object>();
                ViewBag.RecommendedRecipes = new List<object>();
                return View();
            }
        }



        [HttpGet]
        public async Task<IActionResult> RecipeDetails(int id)
        {
            JObject recipeData = new JObject();
            List<Review> reviews = new List<Review>();
            var userId = User.Identity.IsAuthenticated ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
            var isFavorite = false;
            
            if (userId != null)
            {
                _logger.LogInformation($"Checking favorites for userId: {userId}, recipeId: {id}");
                var favorite = await _context.FavoriteRecipes
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.RecipeId == id);
                isFavorite = favorite != null;
                _logger.LogInformation($"Favorite check result: {isFavorite}");
            }

            // Add isFavorite to the recipe data
            recipeData["isFavorite"] = isFavorite;
            _logger.LogInformation($"Recipe data isFavorite set to: {recipeData["isFavorite"]}");

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
                        ["sourceType"] = "database",
                        ["isFavorite"] = isFavorite // Ensure isFavorite is preserved
                    };
                }
                else
                {
                    var apiKey = _spoonacularSettings.ApiKey;

                    // If not in database, try API
                    string apiUrl = $"https://api.spoonacular.com/recipes/{id}/information?apiKey={apiKey}";
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
                            ["sourceType"] = "api",
                            ["isFavorite"] = isFavorite // Ensure isFavorite is preserved
                        };
                    }
                    else
                    {
                        throw new Exception("Recipe not found in API");
                    }
                }
                _logger.LogInformation($"Final recipe data isFavorite: {recipeData["isFavorite"]}");
                var recipeUrl = $"http://localhost:7119/Home/RecipeDetails/{id}";
                ViewBag.RecipeUrl = recipeUrl;
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

[HttpGet]
public async Task<IActionResult> SearchRecipes(string query, string cuisine, string mealType, string diet, int page = 1)
{
    const int PageSize = 12;
    var combinedResults = new List<JToken>();

    // Get user's favorite recipes
    var userId = User.Identity.IsAuthenticated ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    var userFavorites = new HashSet<int>();
    
    if (userId != null)
    {
        var favoriteIds = await _context.FavoriteRecipes
            .Where(f => f.UserId == userId)
            .Select(f => f.RecipeId)
            .ToListAsync();
        userFavorites = new HashSet<int>(favoriteIds);
    }

    // Database search
    var dbQuery = _context.Recipes.AsQueryable();
    if (!string.IsNullOrEmpty(query))
    {
        dbQuery = dbQuery.Where(r => EF.Functions.Like(r.Title, $"%{query}%"));
    }
    if (!string.IsNullOrEmpty(cuisine))
    {
        dbQuery = dbQuery.Where(r => r.CusineType.ToLower() == cuisine.ToLower());
    }
    if (!string.IsNullOrEmpty(mealType))
    {
        dbQuery = dbQuery.Where(r => r.MealType.ToLower() == mealType.ToLower());
    }
    if (!string.IsNullOrEmpty(diet))
    {
        dbQuery = dbQuery.Where(r => r.DietType.ToLower() == diet.ToLower());
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
            ["sourceType"] = "database",
            ["vegetarian"] = recipe.DietType?.ToLower() == "vegetarian",
            ["isFavorite"] = userFavorites.Contains(recipe.Id)
        };
        combinedResults.Add(recipeJson);
    }

    // API search
    var baseUrl = "https://api.spoonacular.com/recipes/complexSearch";
    var queryParams = new Dictionary<string, string>
    {
        ["apiKey"] = _spoonacularSettings.ApiKey,
        ["query"] = query ?? string.Empty,
        ["number"] = "100",
        ["fillIngredients"] = "true",
        ["addRecipeInformation"] = "true",
        ["instructionsRequired"] = "true"
    };

    if (!string.IsNullOrEmpty(cuisine))
    {
        queryParams["cuisine"] = cuisine.ToLower();
    }
    if (!string.IsNullOrEmpty(mealType))
    {
        queryParams["type"] = mealType.ToLower();
    }
    if (!string.IsNullOrEmpty(diet))
    {
        queryParams["diet"] = diet.ToLower();
        if (diet.ToLower() == "vegetarian")
        {
            queryParams["diet"] = "vegetarian";
            queryParams["excludeIngredients"] = string.Join(",", GetNonVegetarianIngredients());
        }
    }

    string url = baseUrl + "?" + string.Join("&", queryParams.Select(p =>
        $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

    try
    {
        var response = await _httpClient.GetStringAsync(url);
        var jsonResponse = JObject.Parse(response);

        if (jsonResponse.ContainsKey("results") && jsonResponse["results"].HasValues)
        {
            var apiRecipes = jsonResponse["results"].ToObject<JArray>();
            foreach (var recipe in apiRecipes)
            {
                if (diet?.ToLower() == "vegetarian")
                {
                    if (!IsVegetarianRecipe(recipe))
                    {
                        continue;
                    }
                }
                recipe["sourceType"] = "api";
                recipe["isFavorite"] = userFavorites.Contains((int)recipe["id"]);
                combinedResults.Add(recipe);
            }
        }

        if (!combinedResults.Any())
        {
            ViewBag.Message = "No recipes found based on your filters.";
            return View("RecipeResults", PaginatedList<JToken>.Create(new List<JToken>(), page, PageSize));
        }

        ViewBag.SearchQuery = query;
        ViewBag.Cuisine = cuisine;
        ViewBag.MealType = mealType;
        ViewBag.Diet = diet;

        var paginatedResults = PaginatedList<JToken>.Create(combinedResults, page, PageSize);
        return View("RecipeResults", paginatedResults);
    }
    catch (HttpRequestException ex)
    {
        _logger.LogError(ex, "API Error in SearchRecipes");
        ViewBag.Message = "Error fetching recipes from API. Showing database results only.";
        var paginatedResults = PaginatedList<JToken>.Create(combinedResults, page, PageSize);
        return View("RecipeResults", paginatedResults);
    }
}


        private string[] GetNonVegetarianIngredients()
        {
            return new[] {
        "chicken", "beef", "pork", "fish", "mutton", "shrimp", "crab", "duck",
        "meat", "bacon", "ham", "turkey", "seafood", "prawn", "gelatin", "lard",
        "rennet", "stock", "bone broth", "anchovy", "worcestershire", "oyster sauce",
        "fish sauce", "shellfish", "squid", "octopus", "lamb", "egg", "eggs",
        "mayonnaise", "caviar", "scallop", "lobster", "cod", "salmon", "tuna",
        "pepperoni", "sausage", "salami", "chorizo", "bone", "veal", "foie gras",
        "liver", "pate", "sardines", "anchovies", "mussels", "clams", "oysters"
    };
        }

        private bool IsVegetarianRecipe(JToken recipe)
        {
            // Check if explicitly marked as vegetarian
            if (recipe["vegetarian"]?.Value<bool>() != true)
            {
                return false;
            }

            // Check title for non-vegetarian keywords
            var title = recipe["title"]?.ToString().ToLower() ?? "";
            if (GetNonVegetarianIngredients().Any(keyword => title.Contains(keyword)))
            {
                return false;
            }

            // Check ingredients
            var ingredients = recipe["extendedIngredients"]?.ToObject<JArray>();
            if (ingredients == null) return true;

            var nonVegKeywords = GetNonVegetarianIngredients();
            return !ingredients.Any(ingredient =>
                nonVegKeywords.Any(keyword =>
                    ingredient["name"]?.ToString().ToLower().Contains(keyword) == true ||
                    ingredient["original"]?.ToString().ToLower().Contains(keyword) == true
                ));
        }
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
[HttpGet]
[Authorize]
public async Task<IActionResult> ShoppingList()
{
    try
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var shoppingList = await _context.ShoppingList
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.DateAdded)
            .ToListAsync();

        var recipeDetails = new List<dynamic>();

        foreach (var item in shoppingList)
        {
            decimal pricePerServing = 0;
            string recipeTitle = "";
            int defaultServings = 1;
            List<string> ingredients = new List<string>();

            var dbRecipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == item.RecipeId);

            if (dbRecipe != null)
            {
                recipeTitle = dbRecipe.Title;
                pricePerServing = dbRecipe.PricePerServing ;
                ingredients = !string.IsNullOrEmpty(dbRecipe.Ingredients)
                    ? dbRecipe.Ingredients.Split(',').Select(i => i.Trim()).ToList()
                    : new List<string>();
            }
            else
            {
                try
                {
                    var apiKey = _spoonacularSettings.ApiKey;
                    var apiUrl = $"https://api.spoonacular.com/recipes/{item.RecipeId}/information?apiKey={apiKey}";
                    var response = await _httpClient.GetStringAsync(apiUrl);
                    var recipe = JObject.Parse(response);

                    recipeTitle = recipe["title"]?.ToString() ?? "";
                    pricePerServing = recipe["pricePerServing"]?.Value<decimal>() ?? 0;
                    
                    var ingredientsList = recipe["extendedIngredients"]?.ToObject<JArray>();
                    if (ingredientsList != null)
                    {
                        ingredients = ingredientsList
                            .Select(i => i["original"]?.ToString() ?? "")
                            .Where(i => !string.IsNullOrEmpty(i))
                            .ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error fetching recipe {item.RecipeId} from API");
                }
            }

            recipeDetails.Add(new
            {
                RecipeId = item.RecipeId,
                RecipeTitle = recipeTitle,
                DateAdded = item.DateAdded,
                Ingredients = ingredients,
                DefaultServings = defaultServings,
                PricePerServing = pricePerServing,
                TotalPrice = pricePerServing * defaultServings,
                Purchased = item.Purchased
            });
        }

        return View(recipeDetails);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error fetching shopping list");
        TempData["Error"] = "An error occurred while loading your shopping list.";
        return View(new List<dynamic>());
    }
}



        [HttpPost]
        [Authorize]
        public async Task<IActionResult> RemoveFromShoppingList(int recipeId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var item = await _context.ShoppingList
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.RecipeId == recipeId);

                if (item != null)
                {
                    _context.ShoppingList.Remove(item);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Recipe removed from shopping list!";
                }
                else
                {
                    TempData["Error"] = "Recipe not found in shopping list.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing recipe from shopping list: {RecipeId}", recipeId);
                TempData["Error"] = "An error occurred while removing the recipe from shopping list.";
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
                    if (!recipeDetails.ContainsKey(review.RecipeId))
                    {
                        var dbRecipe = await _context.Recipes.FindAsync(review.RecipeId);
                        if (dbRecipe != null && !string.IsNullOrEmpty(dbRecipe.Title))
                        {
                            recipeDetails[review.RecipeId] = dbRecipe.Title;
                        }
                        else
                        {
                            using (var httpClient = _httpClient)
                            {
                                var apiKey = _spoonacularSettings.ApiKey;
                                string apiUrl = $"https://api.spoonacular.com/recipes/{review.RecipeId}/information?apiKey={apiKey}";
                                var response = await httpClient.GetAsync(apiUrl);

                                if (response.IsSuccessStatusCode)
                                {
                                    string jsonResponse = await response.Content.ReadAsStringAsync();
                                    var recipeData = JObject.Parse(jsonResponse);
                                    string title = recipeData["title"]?.ToString();

                                    if (!string.IsNullOrEmpty(title))
                                    {
                                        recipeDetails[review.RecipeId] = title;
                                    }
                                    else
                                    {
                                        recipeDetails[review.RecipeId] = "Untitled Recipe";
                                    }
                                }
                                else
                                {
                                    recipeDetails[review.RecipeId] = "Recipe Not Found";
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error fetching recipe details for ID {review.RecipeId}");
                    recipeDetails[review.RecipeId] = "Unavailable Recipe";
                }
            }

            ViewBag.RecipeDetails = recipeDetails;
            return View("~/Views/Admin/AllReviews.cshtml", reviews);
        }
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SubmitReview(int RecipeId, int Rating, string Comment)
        {
            if (User.IsInRole("Admin"))
            {
                TempData["Error"] = "Administrators cannot submit reviews.";
                return RedirectToAction(nameof(RecipeDetails), new { id = RecipeId });
            }
            try
            {
                if (Rating < 1 || Rating > 5)
                {
                    TempData["Error"] = "Rating must be between 1 and 5.";
                    return RedirectToAction(nameof(RecipeDetails), new { id = RecipeId });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var existingReview = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.RecipeId == RecipeId && r.UserId == userId);

                if (existingReview != null)
                {
                    existingReview.Rating = Rating;
                    existingReview.Comment = Comment;
                   // existingReview.DateModified = DateTime.UtcNow;
                    _context.Reviews.Update(existingReview);
                }
                else
                {
                    var review = new Review
                    {
                        RecipeId = RecipeId,
                        UserId = userId,
                        Rating = Rating,
                        Comment = Comment,
                       // DateCreated = DateTime.UtcNow
                    };
                    _context.Reviews.Add(review);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Your review has been submitted successfully!";
                return RedirectToAction(nameof(RecipeDetails), new { id = RecipeId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting review for recipe: {RecipeId}", RecipeId);
                TempData["Error"] = "An error occurred while submitting your review.";
                return RedirectToAction(nameof(RecipeDetails), new { id = RecipeId });
            }
        }
      
        [HttpGet]
        public async Task<IActionResult> GetReviews(int recipeId)
        {
            var reviews = await _context.Reviews
                .Where(r => r.RecipeId == recipeId)
                .Select(r => new
                {
                    r.Id,
                    r.UserId,
                    r.Comment,
                    r.Rating,
                    CanEdit = r.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier)
                })
                .ToListAsync();

            var averageRating = reviews.Any() ?
                Math.Round(reviews.Average(r => r.Rating), 1) : 0;

            return Json(new
            {
                reviews = reviews,
                averageRating = averageRating,
                totalReviews = reviews.Count
            });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> DeleteReview(int reviewId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var review = await _context.Reviews.FindAsync(reviewId);

            if (review == null)
            {
                return Json(new { success = false, message = "Review not found" });
            }

            if (review.UserId != userId && !User.IsInRole("Admin"))
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            var averageRating = await _context.Reviews
                .Where(r => r.RecipeId == review.RecipeId)
                .Select(r => (double?)r.Rating)
                .AverageAsync() ?? 0;

            return Json(new
            {
                success = true,
                message = "Review deleted successfully",
                averageRating = Math.Round(averageRating, 1)
            });
        }
       

       [HttpPost]
[Authorize]
public async Task<IActionResult> AddMissingIngredientsToList(int recipeId)
{
    try
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int defaultServings = 1;

        // Check if recipe already exists in shopping list
        var existingItem = await _context.ShoppingList
            .FirstOrDefaultAsync(i => i.UserId == userId && i.RecipeId == recipeId);

        // Try to get recipe details from database first
        var dbRecipe = await _context.Recipes
            .FirstOrDefaultAsync(r => r.Id == recipeId);

        if (existingItem == null)
        {
            var shoppingItem = new ShoppingListItem
            {
                UserId = userId,
                RecipeId = recipeId,
                Servings = defaultServings,
                DateAdded = DateTime.Now,
                Purchased = false
            };

            _context.ShoppingList.Add(shoppingItem);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Recipe added to shopping list!";
        }
        else
        {
            existingItem.Servings += defaultServings;
            _context.ShoppingList.Update(existingItem);
            await _context.SaveChangesAsync();
            TempData["Info"] = "Updated servings for existing recipe in shopping list.";
        }

        return RedirectToAction(nameof(ShoppingList));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error adding recipe to shopping list: {RecipeId}", recipeId);
        TempData["Error"] = "An error occurred while adding recipe to shopping list.";
        return RedirectToAction(nameof(ShoppingList));
    }
}
        [HttpGet]
public async Task<IActionResult> GetSpellingSuggestions(string query)
{
    if (string.IsNullOrEmpty(query))
        return Json(new string[] { });

    var suggestions = new HashSet<string>();

    // Get suggestions from database
    var dbSuggestions = await _context.Recipes
        .Where(r => r.Title.Contains(query) || 
                    EF.Functions.Like(r.Title, $"%{query}%"))
        .Select(r => r.Title)
        .Take(5)
        .ToListAsync();

    foreach (var suggestion in dbSuggestions)
    {
        suggestions.Add(suggestion);
    }

    // Get suggestions from API
    try
    {
                var apiKey = _spoonacularSettings.ApiKey;

                //string apiKey = "4f23d090497a4cc6942f7f6e1f3ffca4";
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


        public IActionResult Contact()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> SubmitContact(Contact contact)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    contact.DateSubmitted = DateTime.UtcNow;
                    contact.IsRead = false;
                    _context.Contacts.Add(contact);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true });
                }
                return Json(new { success = false, message = "Invalid form data" });
            }
            catch (Exception ex)
            {
                // Log the exception if you have logging configured
                return Json(new { success = false, message = ex.Message });
            }
        }
   [HttpGet]
public async Task<IActionResult> IsFavorite(int recipeId)
{
    if (!User.Identity.IsAuthenticated)
    {
        return Json(new { isFavorite = false });
    }

    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    var isFavorite = await _context.FavoriteRecipes
        .AnyAsync(f => f.UserId == userId && f.RecipeId == recipeId);

    return Json(new { isFavorite });
}

        public async Task<IActionResult> ToggleFavorite(int recipeId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Please login to add favorites" });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var favorite = await _context.FavoriteRecipes
                .FirstOrDefaultAsync(f => f.UserId == userId && f.RecipeId == recipeId);

            if (favorite != null)
            {
                _context.FavoriteRecipes.Remove(favorite);
                await _context.SaveChangesAsync();
                return Json(new { success = true, isFavorite = false, message = "Removed from favorites" });
            }
            else
            {
                // Get recipe details to get the title and image URL
                string title = "";
                string imageUrl = "";
                var dbRecipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == recipeId);
                if (dbRecipe != null)
                {
                    title = dbRecipe.Title ?? "Untitled Recipe";
                    imageUrl = dbRecipe.ImageUrl ?? "";
                }
                else
                {
                    // If not in database, try to get from API
                    try
                    {
                        var apiKey = _spoonacularSettings.ApiKey;
                        var apiUrl = $"https://api.spoonacular.com/recipes/{recipeId}/information?apiKey={apiKey}";
                        var response = await _httpClient.GetStringAsync(apiUrl);
                        var recipeData = JObject.Parse(response);
                        title = recipeData["title"]?.ToString() ?? "Untitled Recipe";
                        imageUrl = recipeData["image"]?.ToString() ?? "";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error fetching recipe details from API");
                        title = "Untitled Recipe";
                        imageUrl = ""; // Use empty string as fallback
                    }
                }

                var newFavorite = new FavoriteRecipe
                {
                    UserId = userId,
                    RecipeId = recipeId,
                    Title = title,
                    ImageUrl = imageUrl,
                    DateAdded = DateTime.UtcNow
                };
                _context.FavoriteRecipes.Add(newFavorite);
                await _context.SaveChangesAsync();
                return Json(new { success = true, isFavorite = true, message = "Added to favorites! ❤️" });
            }
        }
      
        [HttpGet]
[Authorize]
public IActionResult Address()
{
    return View();
}
        // ... existing code ...
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Address(Address model)
        {
            // Set UserId before validation
            model.UserId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);

            // Remove ModelState errors for UserId and User
            ModelState.Remove("UserId");
            ModelState.Remove("User");

            if (!ModelState.IsValid)
                return View(model);

            _context.Addresses.Add(model);
            await _context.SaveChangesAsync();

            // --- Begin: Create Order and OrderItems after saving address ---
            var userId = model.UserId;
            var shoppingList = await _context.ShoppingList
                .Where(s => s.UserId == userId)
                .ToListAsync();

            if (!shoppingList.Any())
            {
                TempData["Error"] = "Your shopping list is empty.";
                return RedirectToAction("ShoppingList");
            }

            // Create new order
            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.Now,
                AddressId = model.Id,
                TotalAmount = shoppingList.Sum(i =>
                {
                    var recipe = _context.Recipes.FirstOrDefault(r => r.Id == i.RecipeId);
                    return (recipe != null ? recipe.PricePerServing : 0) * i.Servings;
                }),
                Status = "Pending",
                OrderNumber = Guid.NewGuid().ToString().Substring(0, 8).ToUpper()
            };
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Add order items
            foreach (var item in shoppingList)
            {
                var recipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == item.RecipeId);
                decimal pricePerServing = recipe != null ? recipe.PricePerServing : 0;

                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    RecipeId = item.RecipeId,
                    Servings = item.Servings,
                    PricePerServings = pricePerServing,
                    TotalPrice = pricePerServing * item.Servings
                };
                _context.OrderItems.Add(orderItem);
            }
            await _context.SaveChangesAsync();

            

            TempData["AddressId"] = model.Id;
            // Redirect to OrderSummary with both addressId and orderId
            return RedirectToAction("OrderSummary", new { addressId = model.Id, orderId = order.Id });
        }
      
        [Authorize]
        public async Task<IActionResult> OrderSummary(int addressId, int orderId, string couponCode = null)
        {
            var recipeDetails = new List<dynamic>();

            // Fetch order items for the given orderId
            var orderItems = await _context.OrderItems
                .Where(oi => oi.OrderId == orderId)
                .ToListAsync();

            // Fetch the address for the given addressId
            var address = await _context.Addresses.FindAsync(addressId);

            decimal totalOrderAmount = 0; // To store the total amount before discount

            foreach (var oi in orderItems)
            {
                string recipeImg = "", recipeTitle = "";
                decimal pricePerServing = oi.PricePerServings;
                int defaultServings = oi.Servings;

                var dbRecipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == oi.RecipeId);

                if (dbRecipe != null)
                {
                    recipeImg = dbRecipe.ImageUrl;
                    recipeTitle = dbRecipe.Title;
                    pricePerServing = dbRecipe.PricePerServing;
                    defaultServings = dbRecipe.Servings;
                }
                else
                {
                    try
                    {
                        var apiKey = _spoonacularSettings.ApiKey;
                        var apiUrl = $"https://api.spoonacular.com/recipes/{oi.RecipeId}/information?apiKey={apiKey}";
                        var response = await _httpClient.GetStringAsync(apiUrl);
                        var recipe = JObject.Parse(response);

                        recipeImg = recipe["image"]?.ToString() ?? "";
                        recipeTitle = recipe["title"]?.ToString() ?? "";
                        pricePerServing = recipe["pricePerServing"]?.Value<decimal>() ?? 0;
                        defaultServings = recipe["servings"]?.Value<int>() ?? 1;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error fetching recipe {oi.RecipeId} from API");
                    }
                }

                decimal totalPrice = pricePerServing * oi.Servings;
                totalOrderAmount += totalPrice;

                recipeDetails.Add(new
                {
                    Img = recipeImg,
                    RecipeId = oi.RecipeId,
                    RecipeTitle = recipeTitle,
                    Servings = oi.Servings,
                    PricePerServing = pricePerServing,
                    TotalPrice = totalPrice,
                });
            }

            // Apply range-based discount logic based on total order amount
            decimal discountAmount = 0;
            string discountDescription = string.Empty;

            if (totalOrderAmount >= 1000 && totalOrderAmount < 2000)
            {
                // Apply 10% discount for orders between 1000 and 2000
                discountAmount = totalOrderAmount * 0.10m;
                discountDescription = "10% off on orders above 1000 INR";
            }
            else if (totalOrderAmount >= 2000 && totalOrderAmount < 5000)
            {
                // Apply 15% discount for orders between 2000 and 5000
                discountAmount = totalOrderAmount * 0.15m;
                discountDescription = "15% off on orders above 2000 INR";
            }
            else if (totalOrderAmount >= 5000)
            {
                // Apply 20% discount for orders above 5000
                discountAmount = totalOrderAmount * 0.20m;
                discountDescription = "20% off on orders above 5000 INR";
            }

            // Ensure discount doesn't exceed the total order amount
            discountAmount = Math.Min(discountAmount, totalOrderAmount);
            totalOrderAmount -= discountAmount;

            // Store the discount information for use in the view
            ViewBag.DiscountAmount = discountAmount;
            ViewBag.DiscountDescription = discountDescription;
            ViewBag.FinalTotal = totalOrderAmount;

            // Return the view with order details, address, and the total amount (after discount)
            return View("OrderSummary", Tuple.Create(recipeDetails, address));
        }

        ////[Authorize]
        //public async Task<IActionResult> OrderSummary(int addressId, int orderId)
        //{
        //    var recipeDetails = new List<dynamic>();

        //    // Fetch order items for the given orderId
        //    var orderItems = await _context.OrderItems
        //        .Where(oi => oi.OrderId == orderId)
        //        .ToListAsync();

        //    // Fetch the address for the given addressId
        //    var address = await _context.Addresses.FindAsync(addressId);

        //    foreach (var oi in orderItems)
        //    {
        //        string recipeImg = "", recipeTitle  = "";
        //        decimal pricePerServing = oi.PricePerServings;
        //        int defaultServings = oi.Servings;

        //        var dbRecipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == oi.RecipeId);

        //        if (dbRecipe != null)
        //        {
        //            recipeImg = dbRecipe.ImageUrl;
        //            recipeTitle = dbRecipe.Title;
        //            pricePerServing = dbRecipe.PricePerServing;
        //            defaultServings = dbRecipe.Servings;
        //        }
        //        else
        //        {
        //            try
        //            {
        //                var apiKey = _spoonacularSettings.ApiKey;
        //                var apiUrl = $"https://api.spoonacular.com/recipes/{oi.RecipeId}/information?apiKey={apiKey}";
        //                var response = await _httpClient.GetStringAsync(apiUrl);
        //                var recipe = JObject.Parse(response);


        //                recipeImg = recipe["image"]?.ToString() ?? "";
        //                recipeTitle = recipe["title"]?.ToString() ?? "";
        //                pricePerServing = recipe["pricePerServing"]?.Value<decimal>() ?? 0;
        //                defaultServings = recipe["servings"]?.Value<int>() ?? 1;
        //            }
        //            catch (Exception ex)
        //            {
        //                _logger.LogError(ex, $"Error fetching recipe {oi.RecipeId} from API");
        //            }
        //        }

        //        recipeDetails.Add(new
        //        {   
        //            Img = recipeImg,
        //            RecipeId = oi.RecipeId,
        //            RecipeTitle = recipeTitle,
        //            Servings = oi.Servings,
        //            PricePerServing = pricePerServing,
        //            TotalPrice = pricePerServing * oi.Servings,
        //        });
        //    }

        //    return View(Tuple.Create(recipeDetails, address));
        //}

        // ... existing code ...
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UpdateServings(int recipeId, int servings)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var item = await _context.ShoppingList
                .FirstOrDefaultAsync(s => s.UserId == userId && s.RecipeId == recipeId);

            if (item == null)
            {
                return Json(new { success = false, message = "Item not found in shopping list." });
            }

            if (servings < 1)
            {
                return Json(new { success = false, message = "Servings must be at least 1." });
            }

            item.Servings = servings;
            _context.ShoppingList.Update(item);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Servings updated successfully." });
        }
       
[Authorize]
public async Task<IActionResult> MyOrders()
{
    var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
    var orders = await _context.Orders
        .Where(o => o.UserId == userId)
        .OrderByDescending(o => o.OrderDate)
        .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Recipe)
        .ToListAsync();

    // Build a view model with all required details
    var ordersVm = new List<dynamic>();
    foreach (var order in orders)
    {
        var orderItemsVm = new List<dynamic>();
        foreach (var item in order.OrderItems)
        {
            string recipeImg = "", recipeTitle = "";
            decimal pricePerServing = item.PricePerServings;
            int servings = item.Servings;
                    var dbRecipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == item.RecipeId);

                    if (dbRecipe != null)
            {
                recipeImg = item.Recipe.ImageUrl;
                recipeTitle = item.Recipe.Title;
                pricePerServing = item.Recipe.PricePerServing;
                servings = item.Servings;
            }
            else
            {
                try
                {
                    var apiKey = _spoonacularSettings.ApiKey;
                    var apiUrl = $"https://api.spoonacular.com/recipes/{item.RecipeId}/information?apiKey={apiKey}";
                    var response = await _httpClient.GetStringAsync(apiUrl);
                    var recipe = JObject.Parse(response);

                    recipeImg = recipe["image"]?.ToString() ?? "";
                    recipeTitle = recipe["title"]?.ToString() ?? "";
                    pricePerServing = recipe["pricePerServing"]?.Value<decimal>() ?? 0;
                    servings = item.Servings;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error fetching recipe {item.RecipeId} from API");
                }
            }

            orderItemsVm.Add(new
            {
                RecipeId = item.RecipeId,
                RecipeTitle = recipeTitle,
                RecipeImg = recipeImg,
                Servings = servings,
                PricePerServing = pricePerServing,
                TotalPrice = pricePerServing * servings
            });
        }

        ordersVm.Add(new
        {
            order.Id,
            order.OrderNumber,
            order.Status,
            order.TotalAmount,
            order.OrderDate,
            OrderItems = orderItemsVm
        });
    }

    return View(ordersVm);
}
// ... existing code ...
[HttpPost]
[Authorize]
public async Task<IActionResult> CancelOrder(int orderId)
{
    var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
    var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

    if (order == null || order.Status == "Shipped" || order.Status == "Delivered" || order.Status == "Cancelled")
    {
        return Json(new { success = false, message = "Order cannot be cancelled." });
    }

    order.Status = "Cancelled";
    _context.Orders.Update(order);
    await _context.SaveChangesAsync();

    return Json(new { success = true, message = "Order cancelled successfully." });
}// ... existing code ...

//[HttpPost]
//[Authorize]
//public async Task<IActionResult> Checkout(int addressId, string couponCode)
//{
//    var coupons = new Dictionary<string, decimal>
//    {
//        { "SAVE10", 0.10m },
//        { "WELCOME5", 0.05m },
//        { "FESTIVE20", 0.20m },
//        { "SUPER30", 0.30m },
//        { "EXTRA50", 0.50m }
//    };

//    decimal discountPercent = 0;
//    string couponMessage = null;
//    if (!string.IsNullOrEmpty(couponCode) && coupons.TryGetValue(couponCode.ToUpper(), out var percent))
//    {
//        discountPercent = percent;
//        couponMessage = $"Coupon '{couponCode}' applied: {percent * 100}% off!";
//    }
//    else if (!string.IsNullOrEmpty(couponCode))
//    {
//        couponMessage = "Invalid or expired coupon code.";
//    }

//    // Pass coupon info to OrderSummary using TempData
//    TempData["CouponMessage"] = couponMessage;
//    TempData["DiscountPercent"] = discountPercent;

//    // You may need to determine the correct orderId here
//    // For demonstration, assuming you have orderId available
//    return RedirectToAction("OrderSummary", new { addressId = addressId /*, orderId = ... */ });
//}
// ... existing code ...

        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult About()
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


