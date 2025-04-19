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
        // public async Task<IActionResult> GetRecipes(string ingredients, int page = 1)
        // {
        //     const int PageSize = 12;
        //     var combinedResults = new List<JToken>();

        //     if (!string.IsNullOrEmpty(ingredients))
        //     {
        //         // Database search
        //         var ingredientList = ingredients.Split(',').Select(i => i.Trim().ToLower()).ToList();
        //         var dbRecipes = await _context.Recipes
        //             .Where(r => r.Ingredients != null &&
        //                 ingredientList.Any(i => r.Ingredients.ToLower().Contains(i)))
        //             .ToListAsync();

        //         foreach (var recipe in dbRecipes)
        //         {
        //             var usedIngredients = ingredientList.Count(i =>
        //                 recipe.Ingredients.ToLower().Contains(i.ToLower()));

        //             var recipeJson = new JObject
        //             {
        //                 ["id"] = recipe.Id,
        //                 ["title"] = recipe.Title,
        //                 ["image"] = recipe.ImageUrl ?? "",
        //                 ["sourceType"] = "database",
        //                 ["readyInMinutes"] = recipe.CookingTime,
        //                 ["servings"] = recipe.Servings,
        //                 ["usedIngredientCount"] = usedIngredients,
        //                 ["missedIngredientCount"] = ingredientList.Count - usedIngredients,
        //                 ["likes"] = 0,
        //                 ["vegetarian"] = recipe.DietType?.ToLower() == "vegetarian"
        //             };
        //             combinedResults.Add(recipeJson);
        //         }

        //         // API search
        //         try
        //         {
        //             var baseUrl = "https://api.spoonacular.com/recipes/findByIngredients";
        //             var queryParams = new Dictionary<string, string>
        //             {
        //                 ["apiKey"] = _spoonacularSettings.ApiKey,
        //                 ["ingredients"] = ingredients,
        //                 ["number"] = "20", // Increased to get more results for pagination
        //                 ["ranking"] = "2",
        //                 ["ignorePantry"] = "true"
        //             };

        //             string url = baseUrl + "?" + string.Join("&", queryParams.Select(p =>
        //                 $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        //             var response = await _httpClient.GetStringAsync(url);
        //             var apiRecipes = JArray.Parse(response);

        //             foreach (var recipe in apiRecipes)
        //             {
        //                 recipe["sourceType"] = "api";
        //                 combinedResults.Add(recipe);
        //             }
        //         }
        //         catch (Exception ex)
        //         {
        //             _logger.LogError(ex, "Error fetching recipes from API");
        //             ViewBag.Message = "Error fetching recipes from API. Showing database results only.";
        //         }

        //         if (!combinedResults.Any())
        //         {
        //             ViewBag.Message = "No recipes found with these ingredients.";
        //             return View("RecipeResults", PaginatedList<JToken>.Create(new List<JToken>(), page, PageSize));
        //         }
        //     }

        //     // Apply pagination to combined results
        //     var totalItems = combinedResults.Count;
        //     var items = combinedResults
        //         .Skip((page - 1) * PageSize)
        //         .Take(PageSize)
        //         .ToList();

        //     var paginatedResults = PaginatedList<JToken>.Create(combinedResults, page, PageSize);
        //     // Store the full results in session
        //     HttpContext.Session.SetString("Recipes", JsonConvert.SerializeObject(combinedResults));
        //     HttpContext.Session.SetInt32("CurrentPage", page);
            
        //     ViewBag.Ingredients = ingredients;
        //     return View("RecipeResults", paginatedResults);
        // }
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
        //public IActionResult RecipeResults()
        //{
        //    var recipesJson = HttpContext.Session.GetString("Recipes");

        //    if (string.IsNullOrEmpty(recipesJson))
        //    {
        //        return RedirectToAction("Index"); // Redirect if session is empty
        //    }

        //    var recipes = JsonConvert.DeserializeObject<JArray>(recipesJson);
        //    return View("Recipes", recipes);
        //}
        // Remove these duplicate RecipeResults methods
        // public async Task<IActionResult> RecipeResults() { ... }
        // public IActionResult RecipeResults() { ... }

        // Keep only this version of RecipeResults
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

        // [HttpGet]
        // public async Task<IActionResult> SearchRecipes(string query, string cuisine, string mealType, string diet, int page = 1)
        // {
        //     const int PageSize = 12;
        //     var combinedResults = new List<JToken>();

        //     // Database search
        //     var dbQuery = _context.Recipes.AsQueryable();
        //     if (!string.IsNullOrEmpty(query))
        //     {
        //         dbQuery = dbQuery.Where(r => EF.Functions.Like(r.Title, $"%{query}%"));
        //     }
        //     if (!string.IsNullOrEmpty(cuisine))
        //     {
        //         dbQuery = dbQuery.Where(r => r.CusineType.ToLower() == cuisine.ToLower());
        //     }
        //     if (!string.IsNullOrEmpty(mealType))
        //     {
        //         dbQuery = dbQuery.Where(r => r.MealType.ToLower() == mealType.ToLower());
        //     }
        //     if (!string.IsNullOrEmpty(diet))
        //     {
        //         dbQuery = dbQuery.Where(r => r.DietType.ToLower() == diet.ToLower());
        //     }

        //     var dbRecipes = await dbQuery.ToListAsync();
        //     foreach (var recipe in dbRecipes)
        //     {
        //         var recipeJson = new JObject
        //         {
        //             ["id"] = recipe.Id,
        //             ["title"] = recipe.Title,
        //             ["image"] = recipe.ImageUrl ?? "",
        //             ["readyInMinutes"] = recipe.CookingTime,
        //             ["servings"] = recipe.Servings,
        //             ["sourceType"] = "database",
        //             ["vegetarian"] = recipe.DietType?.ToLower() == "vegetarian"
        //         };
        //         combinedResults.Add(recipeJson);
        //     }

        //     // API search
        //     var baseUrl = "https://api.spoonacular.com/recipes/complexSearch";
        //     var queryParams = new Dictionary<string, string>
        //     {
        //         ["apiKey"] = _spoonacularSettings.ApiKey,
        //         ["query"] = query ?? string.Empty,
        //         ["number"] = "100",
        //         ["fillIngredients"] = "true",
        //         ["addRecipeInformation"] = "true",
        //         ["instructionsRequired"] = "true"
        //     };

        //     if (!string.IsNullOrEmpty(cuisine))
        //     {
        //         queryParams["cuisine"] = cuisine.ToLower();
        //     }
        //     if (!string.IsNullOrEmpty(mealType))
        //     {
        //         queryParams["type"] = mealType.ToLower();
        //     }
        //     if (!string.IsNullOrEmpty(diet))
        //     {
        //         queryParams["diet"] = diet.ToLower();
        //         if (diet.ToLower() == "vegetarian")
        //         {
        //             queryParams["diet"] = "vegetarian";
        //             queryParams["excludeIngredients"] = string.Join(",", GetNonVegetarianIngredients());
        //         }
        //     }

        //     string url = baseUrl + "?" + string.Join("&", queryParams.Select(p =>
        //         $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        //     try
        //     {
        //         var response = await _httpClient.GetStringAsync(url);
        //         var jsonResponse = JObject.Parse(response);

        //         if (jsonResponse.ContainsKey("results") && jsonResponse["results"].HasValues)
        //         {
        //             var apiRecipes = jsonResponse["results"].ToObject<JArray>();
        //             foreach (var recipe in apiRecipes)
        //             {
        //                 if (diet?.ToLower() == "vegetarian")
        //                 {
        //                     if (!IsVegetarianRecipe(recipe))
        //                     {
        //                         continue;
        //                     }
        //                 }
        //                 recipe["sourceType"] = "api";
        //                 combinedResults.Add(recipe);
        //             }
        //         }

        //         if (!combinedResults.Any())
        //         {
        //             ViewBag.Message = "No recipes found based on your filters.";
        //             return View("RecipeResults", PaginatedList<JToken>.Create(new List<JToken>(), page, PageSize));
        //         }

        //         ViewBag.SearchQuery = query;
        //         ViewBag.Cuisine = cuisine;
        //         ViewBag.MealType = mealType;
        //         ViewBag.Diet = diet;

        //         var paginatedResults = PaginatedList<JToken>.Create(combinedResults, page, PageSize);
        //         return View("RecipeResults", paginatedResults);
        //     }
        //     catch (HttpRequestException ex)
        //     {
        //         _logger.LogError(ex, "API Error in SearchRecipes");
        //         ViewBag.Message = "Error fetching recipes from API. Showing database results only.";
        //         var paginatedResults = PaginatedList<JToken>.Create(combinedResults, page, PageSize);
        //         return View("RecipeResults", paginatedResults);
        //     }
        // }

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
        decimal usdToInrRate = 83.0m;

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
                defaultServings = dbRecipe.Servings;
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
                    pricePerServing = (recipe["pricePerServing"]?.Value<decimal>() ?? 0) * usdToInrRate;
                    defaultServings = recipe["servings"]?.Value<int>() ?? 1;
                    
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
                Servings = item.Servings,
                DefaultServings = defaultServings,
                PricePerServing = pricePerServing,
                TotalPrice = pricePerServing * item.Servings,
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

        //[HttpGet]
        //[Authorize]
        //public async Task<IActionResult> ShoppingList()
        //{
        //    try
        //    {
        //        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //        var shoppingList = await _context.ShoppingList
        //            .Where(s => s.UserId == userId)
        //            .OrderByDescending(s => s.DateAdded)
        //            .ToListAsync();

        //        var recipeIngredients = new List<dynamic>();

        //        foreach (var item in shoppingList)
        //        {
        //            // First try to get recipe from database
        //            var dbRecipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == item.RecipeId);

        //            if (dbRecipe != null)
        //            {
        //                var ingredients = !string.IsNullOrEmpty(dbRecipe.Ingredients)
        //                    ? dbRecipe.Ingredients.Split(',').Select(i => i.Trim()).ToList()
        //                    : new List<string>();

        //                recipeIngredients.Add(new
        //                {
        //                    RecipeId = item.RecipeId,
        //                    RecipeTitle = dbRecipe.Title,
        //                    DateAdded = item.DateAdded,
        //                    Ingredients = ingredients
        //                });
        //            }
        //            else
        //            {
        //                // If not in database, fetch from API
        //                try
        //                {
        //                    var apiKey = _spoonacularSettings.ApiKey;
        //                    var apiUrl = $"https://api.spoonacular.com/recipes/{item.RecipeId}/information?apiKey={apiKey}";
        //                    var response = await _httpClient.GetStringAsync(apiUrl);
        //                    var recipe = JObject.Parse(response);

        //                    recipeIngredients.Add(new
        //                    {
        //                        RecipeId = item.RecipeId,
        //                        RecipeTitle = recipe["title"].ToString(),
        //                        DateAdded = item.DateAdded,
        //                        Ingredients = recipe["extendedIngredients"].Select(i => i["original"].ToString())
        //                    });
        //                }
        //                catch (Exception ex)
        //                {
        //                    _logger.LogError(ex, $"Error fetching recipe {item.RecipeId} from API");
        //                }
        //            }
        //        }

        //        return View(recipeIngredients);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error fetching shopping list");
        //        TempData["Error"] = "An error occurred while loading your shopping list.";
        //        return View(new List<dynamic>());
        //    }
        //}



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
        //        [HttpPost]
        // [Authorize]
        // public async Task<IActionResult> AddMissingIngredientsToList(int recipeId, int servings = 1)
        // {
        //     try
        //     {
        //         var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        //         // Check if recipe already exists in shopping list
        //         var existingItem = await _context.ShoppingList
        //             .FirstOrDefaultAsync(i => i.UserId == userId && i.RecipeId == recipeId);

        //         if (existingItem == null)
        //         {
        //             decimal pricePerServing = 0;

        //             // Try to get price from database first
        //             var dbRecipe = await _context.Recipes
        //                 .FirstOrDefaultAsync(r => r.Id == recipeId);

        //             if (dbRecipe != null)
        //             {
        //                 pricePerServing = dbRecipe.PricePerServing;
        //             }
        //             else
        //             {
        //                 // If not in database, get from API
        //                 try
        //                 {
        //                     var apiKey = _spoonacularSettings.ApiKey;
        //                     var apiUrl = $"https://api.spoonacular.com/recipes/{recipeId}/information?apiKey={apiKey}";
        //                     var response = await _httpClient.GetStringAsync(apiUrl);
        //                     var recipeData = JObject.Parse(response);
        //                     pricePerServing = recipeData["pricePerServing"]?.Value<decimal>() ?? 0;
        //                 }
        //                 catch (Exception ex)
        //                 {
        //                     _logger.LogError(ex, "Error fetching recipe price from API");
        //                 }
        //             }

        //             var shoppingItem = new ShoppingListItem
        //             {
        //                 UserId = userId,
        //                 RecipeId = recipeId,
        //                 Servings = servings,
        //                 DateAdded = DateTime.Now,
        //                 Purchased = false
        //             };

        //             _context.ShoppingList.Add(shoppingItem);
        //             await _context.SaveChangesAsync();
        //             TempData["Success"] = "Recipe added to shopping list!";
        //         }
        //         else
        //         {
        //             existingItem.Servings += servings;
        //             _context.ShoppingList.Update(existingItem);
        //             await _context.SaveChangesAsync();
        //             TempData["Info"] = "Updated servings for existing recipe in shopping list.";
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Error adding recipe to shopping list: {RecipeId}", recipeId);
        //         TempData["Error"] = "An error occurred while adding recipe to shopping list.";
        //     }

        //     return RedirectToAction(nameof(RecipeDetails), new { id = recipeId });
        // }

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

        if (dbRecipe != null)
        {
            defaultServings = dbRecipe.Servings;
        }
        else
        {
            // If not in database, get from API
            try
            {
                var apiKey = _spoonacularSettings.ApiKey;
                var apiUrl = $"https://api.spoonacular.com/recipes/{recipeId}/information?apiKey={apiKey}";
                var response = await _httpClient.GetStringAsync(apiUrl);
                var recipeData = JObject.Parse(response);
                defaultServings = recipeData["servings"]?.Value<int>() ?? 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching recipe details from API");
            }
        }

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
        //[HttpPost]
        //[Authorize] // Add authorization attribute
        //public async Task<IActionResult> ToggleFavorite(int recipeId)
        //{
        //    try
        //    {
        //        if (!User.Identity.IsAuthenticated)
        //        {
        //            return Json(new { success = false, message = "Please login to add favorites" });
        //        }

        //        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //        if (string.IsNullOrEmpty(userId))
        //        {
        //            return Json(new { success = false, message = "User ID not found" });
        //        }

        //        var existingFavorite = await _context.FavoriteRecipes
        //            .FirstOrDefaultAsync(f => f.UserId == userId && f.RecipeId == recipeId);

        //        if (existingFavorite == null)
        //        {
        //            string title, imageUrl;
        //            bool isExternal = false;

        //            // Try to get recipe from database first
        //            var dbRecipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == recipeId);
        //            if (dbRecipe != null)
        //            {
        //                title = dbRecipe.Title;
        //                imageUrl = dbRecipe.ImageUrl ?? "";
        //            }
        //            else
        //            {
        //                // If not in database, get from API
        //                var apiKey = _spoonacularSettings.ApiKey;
        //                var apiUrl = $"https://api.spoonacular.com/recipes/{recipeId}/information?apiKey={apiKey}";
        //                var response = await _httpClient.GetStringAsync(apiUrl);
        //                var recipeData = JObject.Parse(response);

        //                title = recipeData["title"]?.ToString() ?? "Untitled Recipe";
        //                imageUrl = recipeData["image"]?.ToString() ?? "";
        //                isExternal = true;
        //            }

        //            var favorite = new FavoriteRecipe
        //            {
        //                UserId = userId,
        //                RecipeId = recipeId,
        //                Title = title,
        //                ImageUrl = imageUrl,
        //                IsExternalRecipe = isExternal,
        //                DateAdded = DateTime.UtcNow
        //            };

        //            _context.FavoriteRecipes.Add(favorite);
        //            await _context.SaveChangesAsync();
        //            return Json(new { success = true, isFavorite = true, message = "Added to favorites" });
        //        }
        //        else
        //        {
        //            _context.FavoriteRecipes.Remove(existingFavorite);
        //            await _context.SaveChangesAsync();
        //            return Json(new { success = true, isFavorite = false, message = "Removed from favorites" });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error toggling favorite for recipe: {RecipeId}", recipeId);
        //        return Json(new { success = false, message = "An error occurred while updating favorites" });
        //    }
        //}

        //[HttpGet]
        //[Authorize] // Add authorization attribute
        //public async Task<IActionResult> IsFavorite(int recipeId)
        //{
        //    try
        //    {
        //        if (!User.Identity.IsAuthenticated)
        //        {
        //            return Json(new { isFavorite = false });
        //        }

        //        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //        if (string.IsNullOrEmpty(userId))
        //        {
        //            return Json(new { isFavorite = false });
        //        }

        //        var isFavorite = await _context.FavoriteRecipes
        //            .AnyAsync(f => f.UserId == userId && f.RecipeId == recipeId);
        //        return Json(new { isFavorite = isFavorite });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error checking favorite status for recipe: {RecipeId}", recipeId);
        //        return Json(new { isFavorite = false });
        //    }
        //}
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


