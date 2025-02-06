

using System.Net.Http;
using System.Threading.Tasks;

namespace SmartRecipGene.Services
{
    public class SpoonacularService
    {
        private readonly HttpClient _httpClient;

        private const string ApiKey = "4f23d090497a4cc6942f7f6e1f3ffca4";
        private const string BaseUrl = "https://api.spoonacular.com/";

        public SpoonacularService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetRecipesByIngredientsAsync(string ingredients)
        {
            var url = $"{BaseUrl}recipes/findByIngredients?ingredients={ingredients}&apiKey={ApiKey}";
            var response = await _httpClient.GetStringAsync(url);
            return response;
        }
        public async Task<string> GetRecipeDetailsAsync(int recipeId)
        {
            var url = $"{BaseUrl}recipes/{recipeId}/information?apiKey={ApiKey}";
            var response = await _httpClient.GetStringAsync(url);
            return response;
        }

    }
}

