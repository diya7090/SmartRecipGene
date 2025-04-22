using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using SmartRecipGene.Models;

namespace SmartRecipGene.Services
{
    public class RecipeService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public RecipeService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Spoonacular:ApiKey"];
            _httpClient.BaseAddress = new Uri("https://api.spoonacular.com/");
        }

        public async Task<IEnumerable<RecipeSearchResult>> SearchRecipes(string query)
        {
            var response = await _httpClient.GetAsync(
                $"recipes/complexSearch?query={Uri.EscapeDataString(query)}&apiKey={_apiKey}&number=10");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var searchResult = JsonSerializer.Deserialize<SpoonacularSearchResponse>(content);
            
            return searchResult.Results;
        }
    }
}