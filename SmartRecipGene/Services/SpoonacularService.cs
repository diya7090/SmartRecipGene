
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using SmartRecipGene.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;

namespace SmartRecipGene.Services
{
    public class SpoonacularService : ISpoonacularService
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;
        private const string ApiKey = "b535a4c67f554ae5bb0479ee64a3ac94";
        private const string BaseUrl = "https://api.spoonacular.com/";

        public SpoonacularService(HttpClient httpClient, ApplicationDbContext context)
        {
            _httpClient = httpClient;
            _context = context;
        }

        // Implementing ISpoonacularService methods
    

    //public class SpoonacularService
    //{
    //    private readonly HttpClient _httpClient;
    //    private readonly ApplicationDbContext _context;

    //    private const string ApiKey = "b535a4c67f554ae5bb0479ee64a3ac94";
    //    private const string BaseUrl = "https://api.spoonacular.com/";

    //    public SpoonacularService(HttpClient httpClient, ApplicationDbContext context)
    //    {
    //        _httpClient = httpClient;
    //        _context = context;
    //    }

    // ✅ Get Recipes by Ingredients (Database + API)
    public async Task<string> GetRecipesByIngredientsAsync(string ingredients)
        {
            var combinedResults = new JArray();

            // 🔹 Fetch Database Recipes
            if (!string.IsNullOrEmpty(ingredients))
            {
                var ingredientList = ingredients.Split(',').Select(i => i.Trim().ToLower());
                var dbRecipes = await _context.Recipes
                    .Where(r => ingredientList.Any(i => r.Ingredients.ToLower().Contains(i)))
                    .ToListAsync();

                foreach (var recipe in dbRecipes)
                {
                    var recipeJson = new JObject
                    {
                        ["id"] = recipe.Id,
                        ["title"] = recipe.Title,
                        ["image"] = recipe.ImageUrl ?? "",
                        ["usedIngredientCount"] = ingredientList.Count(i => recipe.Ingredients.ToLower().Contains(i.ToLower())),
                        ["missedIngredientCount"] = ingredientList.Count(i => !recipe.Ingredients.ToLower().Contains(i.ToLower())),
                        ["sourceType"] = "database"
                    };
                    combinedResults.Add(recipeJson);
                }
            }

            // 🔹 Fetch API Recipes
            var url = $"{BaseUrl}recipes/findByIngredients?ingredients={ingredients}&apiKey={ApiKey}&number=10&ranking=2";
            var apiResponse = await _httpClient.GetStringAsync(url);
            var apiRecipes = JArray.Parse(apiResponse);

            foreach (var recipe in apiRecipes)
            {
                recipe["sourceType"] = "api";
                combinedResults.Add(recipe);
            }

            return combinedResults.ToString();
        }

        // ✅ Get Recipe Details from API
        public async Task<string> GetRecipeDetailsAsync(int recipeId)
        {
            var url = $"{BaseUrl}recipes/{recipeId}/information?apiKey={ApiKey}";
            var response = await _httpClient.GetStringAsync(url);
            return response;
        }

        // ✅ Fetch Top-Rated Recipes from API
        public async Task<List<JObject>> GetTopRatedRecipes()
        {
            var url = $"{BaseUrl}recipes/random?number=5&apiKey={ApiKey}";
            var response = await _httpClient.GetStringAsync(url);

            // Parse response as JObject (because it's an object, not an array)
            var apiResponse = JObject.Parse(response);

            // Check if 'recipes' key exists
            if (apiResponse.ContainsKey("recipes"))
            {
                var recipesArray = (JArray)apiResponse["recipes"];

                return recipesArray.Select(r => new JObject
                {
                    ["id"] = r["id"],
                    ["title"] = r["title"],
                    ["image"] = r["image"],
                    ["sourceType"] = "api"
                }).ToList();
            }

            return new List<JObject>(); // Return empty list if no valid data is found
        }


        // ✅ Get Total Number of Recipes in API (Using Random Endpoint)
        public async Task<int> GetTotalRecipesCount()
        {
            var url = $"{BaseUrl}recipes/random?number=1&apiKey={ApiKey}";
            var response = await _httpClient.GetStringAsync(url);

            // Parse the response as JObject (because it's an object, not an array)
            var apiResponse = JObject.Parse(response);

            // Check if it contains the 'recipes' array
            if (apiResponse.ContainsKey("recipes"))
            {
                var recipesArray = (JArray)apiResponse["recipes"];
                return recipesArray.Count;
            }

            return 0; // Return 0 if no valid data is found
        }

    }
}
