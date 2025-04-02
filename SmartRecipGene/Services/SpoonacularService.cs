

//using System.Net.Http;
//using System.Threading.Tasks;

//namespace SmartRecipGene.Services
//{
//    public class SpoonacularService
//    {
//        private readonly HttpClient _httpClient;

//       private const string ApiKey = "b535a4c67f554ae5bb0479ee64a3ac94";

//        // private const string ApiKey = "4f23d090497a4cc6942f7f6e1f3ffca4";
//        private const string BaseUrl = "https://api.spoonacular.com/";

//        public SpoonacularService(HttpClient httpClient)
//        {
//            _httpClient = httpClient;
//        }

//        public async Task<string> GetRecipesByIngredientsAsync(string ingredients)
//        {
//            var url = $"{BaseUrl}recipes/findByIngredients?ingredients={ingredients}&apiKey={ApiKey}";
//            var response = await _httpClient.GetStringAsync(url);
//            return response;
//        }
//        public async Task<string> GetRecipeDetailsAsync(int recipeId)
//        {
//            var url = $"{BaseUrl}recipes/{recipeId}/information?apiKey={ApiKey}";
//            var response = await _httpClient.GetStringAsync(url);
//            return response;
//        }


//    }
//}



using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using SmartRecipGene.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace SmartRecipGene.Services
{
    public class SpoonacularService
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context; // Add this

        private const string ApiKey = "b535a4c67f554ae5bb0479ee64a3ac94";
        private const string BaseUrl = "https://api.spoonacular.com/";

        public SpoonacularService(
            HttpClient httpClient,
            ApplicationDbContext context) // Add context parameter
        {
            _httpClient = httpClient;
            _context = context;
        }

        public async Task<string> GetRecipesByIngredientsAsync(string ingredients)
        {
            var combinedResults = new JArray();

            // Get database recipes
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
                        ["usedIngredientCount"] = ingredientList.Count(i =>
                            recipe.Ingredients.ToLower().Contains(i.ToLower())),
                        ["missedIngredientCount"] = ingredientList.Count(i =>
                            !recipe.Ingredients.ToLower().Contains(i.ToLower())),
                        ["sourceType"] = "database"
                    };
                    combinedResults.Add(recipeJson);
                }
            }

            // Get API recipes
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

        public async Task<string> GetRecipeDetailsAsync(int recipeId)
        {
            var url = $"{BaseUrl}recipes/{recipeId}/information?apiKey={ApiKey}";
            var response = await _httpClient.GetStringAsync(url);
            return response;
        }
    }
}