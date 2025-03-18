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
            var apiKey = _spoonacularSettings.ApiKey;

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


        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddToShoppingList(string ingredient, int recipeId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check for existing ingredient
            var existingItem = await _context.ShoppingList
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Ingredient.ToLower() == ingredient.ToLower());

            if (existingItem == null)
            {
                var shoppingItem = new ShoppingListItem
                {
                    UserId = userId,
                    Ingredient = ingredient,
                    Purchased = false
                };
                _context.ShoppingList.Add(shoppingItem);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{ingredient} added to shopping list!";
            }
            else
            {
                TempData["Info"] = $"{ingredient} is already in your shopping list.";
            }

            return RedirectToAction(nameof(RecipeDetails), new { id = recipeId });
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
                TempData["Success"] = $"{item.Ingredient} marked as purchased!";
            }

            return RedirectToAction("ShoppingList");
        }

        //[HttpPost]
        //[Authorize]
        //public async Task<IActionResult> RemoveFromShoppingList(int id)
        //{
        //    var item = await _context.ShoppingList.FindAsync(id);
        //    if (item != null)  // ✅ Add null check to avoid CS8602
        //    {
        //        _context.ShoppingList.Remove(item);
        //        await _context.SaveChangesAsync();
        //    }
        //    return RedirectToAction("ShoppingList");
        //}

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> RemoveFromShoppingList(int id)
        {
            var item = await _context.ShoppingList.FindAsync(id);
            if (item != null)
            {
                _context.ShoppingList.Remove(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{item.Ingredient} removed from shopping list!";
            }
            return RedirectToAction("ShoppingList");
        }
        //[HttpGet]
        //[Authorize(Roles = "Admin")]
        //public async Task<IActionResult> AllReviews()
        //{
        //    var reviews = await _context.Reviews
        //        .OrderByDescending(r => r.Id)
        //        .ToListAsync();

        //    var recipeDetails = new Dictionary<int, string>();

        //    foreach (var review in reviews)
        //    {
        //        try
        //        {
        //            if (!recipeDetails.ContainsKey(review.RecipeId))  // Only fetch if not already in dictionary
        //            {
        //                using (var httpClient = new HttpClient())
        //                {
        //                    var apiKey = _spoonacularSettings.ApiKey;

        //                    string apiUrl = $"https://api.spoonacular.com/recipes/{review.RecipeId}/information?apiKey={apiKey}";
        //                    var response = await httpClient.GetAsync(apiUrl);

        //                    //if (response.IsSuccessStatusCode)
        //                    //{
        //                    //    string jsonResponse = await response.Content.ReadAsStringAsync();
        //                    //    var recipeData = JObject.Parse(jsonResponse);
        //                    //    recipeDetails[review.RecipeId] = recipeData["title"]?.ToString() ?? $"Recipe {review.RecipeId}";
        //                    //}
        //                    //else
        //                    //{
        //                    //    recipeDetails[review.RecipeId] = $"Recipe {review.RecipeId}";
        //                    //}
        //                }
        //            }
        //        }
        //        catch (Exception)
        //        {
        //            recipeDetails[review.RecipeId] = $"Recipe {review.RecipeId}";
        //        }
        //    }

        //    ViewBag.RecipeDetails = recipeDetails;
        //    return View("~/Views/Admin/AllReviews.cshtml", reviews);
        //}
        // [HttpGet]--------final recipe
        //[Authorize(Roles = "Admin")]
        //public async Task<IActionResult> AllReviews()
        //{
        //    var reviews = await _context.Reviews
        //        .OrderByDescending(r => r.Id)
        //        .ToListAsync();

        //    var recipeDetails = new Dictionary<int, string>();

        //    foreach (var review in reviews)
        //    {
        //        try
        //        {
        //            if (!recipeDetails.ContainsKey(review.RecipeId))
        //            {
        //                // First check if we have the recipe in our database
        //                var dbRecipe = await _context.Recipes.FindAsync(review.RecipeId);
        //                if (dbRecipe != null && !string.IsNullOrEmpty(dbRecipe.Title))
        //                {
        //                    recipeDetails[review.RecipeId] = dbRecipe.Title;
        //                    continue; // Skip API call if we found it in database
        //                }

        //                using (var httpClient = _httpClient)
        //                {
        //                    var apiKey = _spoonacularSettings.ApiKey;
        //                    string apiUrl = $"https://api.spoonacular.com/recipes/{review.RecipeId}/information?apiKey={apiKey}";
        //                    var response = await httpClient.GetAsync(apiUrl);

        //                    if (response.IsSuccessStatusCode)
        //                    {
        //                        string jsonResponse = await response.Content.ReadAsStringAsync();
        //                        var recipeData = JObject.Parse(jsonResponse);
        //                        string title = recipeData["title"]?.ToString();

        //                        if (!string.IsNullOrEmpty(title))
        //                        {
        //                            recipeDetails[review.RecipeId] = title;

        //                            // Update or add to database
        //                            if (dbRecipe == null)
        //                            {
        //                                _context.Recipes.Add(new Recipe
        //                                {
        //                                    Id = review.RecipeId,
        //                                    Title = title
        //                                });
        //                            }
        //                            else
        //                            {
        //                                dbRecipe.Title = title;
        //                                _context.Recipes.Update(dbRecipe);
        //                            }
        //                            await _context.SaveChangesAsync();
        //                        }
        //                        else
        //                        {
        //                            recipeDetails[review.RecipeId] = $"Recipe {review.RecipeId}";
        //                        }
        //                    }
        //                    else
        //                    {
        //                        recipeDetails[review.RecipeId] = $"Recipe {review.RecipeId}";
        //                    }
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, $"Error fetching recipe details for ID {review.RecipeId}");
        //            recipeDetails[review.RecipeId] = $"Recipe {review.RecipeId}";
        //        }
        //    }

        //    ViewBag.RecipeDetails = recipeDetails;
        //    return View("~/Views/Admin/AllReviews.cshtml", reviews);
        //}
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
                var apiKey = _spoonacularSettings.ApiKey;

                // Get recipe details from API
                string apiUrl = $"https://api.spoonacular.com/recipes/{recipeId}/information?apiKey={apiKey}";
                var response = await _httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var recipe = JObject.Parse(content);
                    var ingredients = recipe["extendedIngredients"];

                    foreach (var ingredient in ingredients)
                    {
                        var ingredientName = ingredient["original"].ToString();

                        // Check if ingredient already exists in shopping list
                        var existingItem = await _context.ShoppingList
                            .FirstOrDefaultAsync(i => i.UserId == userId &&
                                                    i.Ingredient.ToLower() == ingredientName.ToLower());

                        if (existingItem == null)
                        {
                            var shoppingItem = new ShoppingListItem
                            {
                                UserId = userId,
                                Ingredient = ingredientName,
                                Purchased = false
                            };
                            _context.ShoppingList.Add(shoppingItem);
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Missing ingredients added to shopping list!";
                }
                else
                {
                    TempData["Error"] = "Could not fetch recipe ingredients.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding missing ingredients for recipe: {RecipeId}", recipeId);
                TempData["Error"] = "An error occurred while adding ingredients to shopping list.";
            }

            return RedirectToAction(nameof(RecipeDetails), new { id = recipeId });
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
            // if (ModelState.IsValid)
            // {
            //     _context.Contacts.Add(contact);
            //     await _context.SaveChangesAsync();
            //     return Json(new { success = true, message = "Thank you for your message!" });
            // }
            // return Json(new { success = false, message = "Please check your input and try again." });
        }

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


