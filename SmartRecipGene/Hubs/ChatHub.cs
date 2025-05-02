
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using SmartRecipGene.Services; // For OpenAIService
using SmartRecipGene.Data;     // For ApplicationDbContext
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;

namespace SmartRecipGene.Hubs
{
    public class ChatHub : Hub
    {
        // private readonly OpenAIService _openAI;
        private readonly ApplicationDbContext _context;
        private readonly HuggingFaceService _huggingFaceService;
        public ChatHub(ApplicationDbContext context, HuggingFaceService huggingFaceService)
        {
            _context = context;
            _huggingFaceService = huggingFaceService;
        }

        public async Task SendMessage(string message)
        {
            try
            {
                var lowerMsg = message.Trim().ToLower();
                var greetings = new[] { "hi", "hii", "hello", "good morning", "good afternoon", "good evening", "hey", "hyee"};
                if (greetings.Any(g => lowerMsg.Equals(g)))
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Hello! How can I help you today? Please ask me anything about recipes or ingredients.");
                    return;
                }

                // Remove "recipe" from the search term for better matching
                var searchTerm = message.Replace("recipe", "", StringComparison.OrdinalIgnoreCase).Trim();

                var dbRecipes = await _context.Recipes
                    .Where(r =>
                        EF.Functions.Like(r.Title, $"%{searchTerm}%") ||
                        EF.Functions.Like(r.Ingredients, $"%{searchTerm}%")
                    )
                    .Select(r => new { r.Id, r.Title, r.Ingredients, r.Instructions })
                    .ToListAsync();

                var spoonacularRecipes = await GetSpoonacularRecipesAsync(message);

                // Show detailed info and link if exactly one DB recipe matches
                if (dbRecipes.Count == 1)
                {
                    var r = dbRecipes.First();
                    var shortInfo = !string.IsNullOrEmpty(r.Instructions)
                        ? r.Instructions.Substring(0, Math.Min(200, r.Instructions.Length)) + (r.Instructions.Length > 200 ? "..." : "")
                        : r.Ingredients.Substring(0, Math.Min(200, r.Ingredients.Length)) + (r.Ingredients.Length > 200 ? "..." : "");
                    var link = $"/Home/RecipeDetails/{r.Id}";
                    var reply = $"<b>{r.Title}</b><br>{shortInfo}<br><a href='{link}'>View Full Recipe</a>";
                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
                    return;
                }
                // If multiple DB recipes match, list them with links
                else if (dbRecipes.Count > 1)
                {
                    var dbTitles = dbRecipes.Select(r => $"- <a href='/Home/RecipeDetails/{r.Id}'>{r.Title}</a>");
                    var spoonTitles = spoonacularRecipes.Select(r =>
                    {
                        var idStart = r.IndexOf("Id: ") + 4;
                        var idEnd = r.IndexOf(",", idStart);
                        var id = (idStart >= 0 && idEnd > idStart) ? r.Substring(idStart, idEnd - idStart) : "";
                        var titleStart = r.IndexOf("Title: ") + 7;
                        var titleEnd = r.IndexOf(",", titleStart);
                        var title = (titleStart >= 0 && titleEnd > titleStart) ? r.Substring(titleStart, titleEnd - titleStart) : r;
                        return $"- <a href='/Home/RecipeDetails/{id}'>{title}</a>";
                    });

                    var allTitles = dbTitles.Concat(spoonTitles).ToList();

                    if (!allTitles.Any())
                    {
                        await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Sorry, I couldn't find any recipes matching your request from either source.");
                        return;
                    }

                    var reply = "Here are some recipes I found:<br>" + string.Join("<br>", allTitles);
                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
                    return;
                }
                // If only Spoonacular recipes found, show the first one with a short info
                else if (dbRecipes.Count == 0 && spoonacularRecipes.Count > 0)
                {
                    var first = spoonacularRecipes.First();
                    var idStart = first.IndexOf("Id: ") + 4;
                    var idEnd = first.IndexOf(",", idStart);
                    var id = (idStart >= 0 && idEnd > idStart) ? first.Substring(idStart, idEnd - idStart) : "";
                    var titleStart = first.IndexOf("Title: ") + 7;
                    var titleEnd = first.IndexOf(",", titleStart);
                    var title = (titleStart >= 0 && titleEnd > titleStart) ? first.Substring(titleStart, titleEnd - titleStart) : first;
                    var infoStart = first.IndexOf("Instructions: ");
                    var info = infoStart > 0 ? first.Substring(infoStart) : first;
                    var shortInfo = info.Length > 200 ? info.Substring(0, 200) + "..." : info;
                    var link = $"/Home/RecipeDetails/{id}";
                    var reply = $"<b>{title}</b><br>{shortInfo}<br><a href='{link}'>View Full Recipe</a>";
                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
                    return;
                }

                // If nothing found, fallback to HuggingFace or show not found
                var allRecipes = dbRecipes
                    .Select(r => $"Title: {r.Title}, Ingredients: {r.Ingredients}, Instructions: {r.Instructions}")
                    .Concat(spoonacularRecipes)
                    .ToList();

                if (!allRecipes.Any())
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Sorry, I couldn't find any recipes matching your request from either source.");
                    return;
                }

                var recipeData = string.Join("\n\n", allRecipes);
                var response = await _huggingFaceService.GetResponse(message, recipeData);
                await Clients.Caller.SendAsync("ReceiveMessage", "Bot", response);
            }
            catch (System.Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "Bot", $"Error: {ex.Message}");
            }
        }

        private async Task<List<string>> GetSpoonacularRecipesAsync(string query)
        {
            var apiKey = "4f23d090497a4cc6942f7f6e1f3ffca4"; // Replace with your actual key
            var url = $"https://api.spoonacular.com/recipes/complexSearch?query={System.Net.WebUtility.UrlEncode(query)}&number=5&addRecipeInformation=true&apiKey={apiKey}";

            using (var httpClient = new System.Net.Http.HttpClient())
            {
                try
                {
                    var response = await httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                        return new List<string>();

                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var results = doc.RootElement.GetProperty("results");
                    var recipes = new List<string>();

                    foreach (var item in results.EnumerateArray())
                    {
                        var id = item.GetProperty("id").GetInt32();
                        var title = item.GetProperty("title").GetString();
                        var ingredients = item.TryGetProperty("extendedIngredients", out var ingArr)
                            ? string.Join(", ", ingArr.EnumerateArray().Select(i => i.GetProperty("original").GetString()))
                            : "";
                        var instructions = item.TryGetProperty("instructions", out var instr) ? instr.GetString() : "";

                        // Include the id in the string for later parsing
                        recipes.Add($"Id: {id}, Title: {title}, Ingredients: {ingredients}, Instructions: {instructions}");
                    }
                    return recipes;
                }
                catch
                {
                    return new List<string>();
                }
            }
        }
    }
}