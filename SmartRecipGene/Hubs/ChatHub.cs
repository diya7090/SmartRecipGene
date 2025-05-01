
//using Microsoft.AspNetCore.SignalR;
//using System.Threading.Tasks;
//using SmartRecipGene.Services; // For OpenAIService
//using SmartRecipGene.Data;     // For ApplicationDbContext
//using Microsoft.EntityFrameworkCore;
//using System.Linq;
//using System.Collections.Generic;

//namespace SmartRecipGene.Hubs
//{
//    public class ChatHub : Hub
//    {
//        // private readonly OpenAIService _openAI;
//        private readonly ApplicationDbContext _context;
//        private readonly HuggingFaceService _huggingFaceService;
//        public ChatHub(ApplicationDbContext context, HuggingFaceService huggingFaceService)
//        {
//            _context = context;
//            _huggingFaceService = huggingFaceService;
//        }
//        // public ChatHub(OpenAIService openAI, ApplicationDbContext context)
//        // {
//        //     _openAI = openAI;
//        //     _context = context;
//        // }

//        public async Task SendMessage(string message)
//        {
//            try
//            {
//                var lowerMsg = message.Trim().ToLower();
//                // ... existing code ...
//                var greetings = new[] { "hi", "hii", "hello", "good morning", "good afternoon", "good evening", "hey" };
//                if (greetings.Any(g => lowerMsg.Equals(g)))
//                {
//                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Hello! How can I help you today? Please ask me anything about recipes or ingredients.");
//                    return;
//                }
//                // ... existing code ...
//                // Expanded greetings list
//                //var greetings = new[] { "hi", "hii", "hello", "good morning", "good afternoon", "good evening", "hey" };
//                //if (greetings.Any(g => lowerMsg.Contains(g)))
//                //{
//                //    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Hello! How can I help you today? Please ask me anything about recipes or ingredients.");
//                //    return;
//                //}

//                var dbRecipes = await _context.Recipes
//    .Where(r =>
//        EF.Functions.Like(r.Title, $"%{message}%") ||
//        EF.Functions.Like(r.Ingredients, $"%{message}%")
//    )
//    .Select(r => new { r.Id, r.Title, r.Ingredients, r.Instructions })
//    .ToListAsync();

//                var spoonacularRecipes = await GetSpoonacularRecipesAsync(message);

//                if (lowerMsg.Contains("suggest") || lowerMsg.Contains("recipe"))
//                {
//                    var dbTitles = dbRecipes.Select(r => $"- {r.Title} (View: /Recipe/Details/{r.Id})");
//                    var spoonTitles = spoonacularRecipes.Select(r =>
//                    {
//                        var titleStart = r.IndexOf("Title: ") + 7;
//                        var titleEnd = r.IndexOf(",", titleStart);
//                        var title = (titleStart >= 0 && titleEnd > titleStart) ? r.Substring(titleStart, titleEnd - titleStart) : r;
//                        return $"- {title}";
//                    });

//                    var allTitles = dbTitles.Concat(spoonTitles).ToList();

//                    if (!allTitles.Any())
//                    {
//                        await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Sorry, I couldn't find any recipes matching your request from either source.");
//                        return;
//                    }

//                    var reply = "Here are some recipes I found:\n" + string.Join("\n", allTitles);
//                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
//                    return;
//                }

//                var allRecipes = dbRecipes
//                    .Select(r => $"Title: {r.Title}, Ingredients: {r.Ingredients}, Instructions: {r.Instructions}")
//                    .Concat(spoonacularRecipes)
//                    .ToList();

//                if (!allRecipes.Any())
//                {
//                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Sorry, I couldn't find any recipes matching your request from either source.");
//                    return;
//                }

//                var recipeData = string.Join("\n\n", allRecipes);
//                var response = await _huggingFaceService.GetResponse(message, recipeData);
//                await Clients.Caller.SendAsync("ReceiveMessage", "Bot", response);
//            }
//            catch (System.Exception ex)
//            {
//                await Clients.Caller.SendAsync("ReceiveMessage", "Bot", $"Error: {ex.Message}");
//            }
//        }

//        private async Task<List<string>> GetSpoonacularRecipesAsync(string query)
//        {
//            var apiKey = "b535a4c67f554ae5bb0479ee64a3ac94"; // Replace with your actual key
//            var url = $"https://api.spoonacular.com/recipes/complexSearch?query={System.Net.WebUtility.UrlEncode(query)}&number=5&addRecipeInformation=true&apiKey={apiKey}";

//            using (var httpClient = new System.Net.Http.HttpClient())
//            {
//                try
//                {
//                    var response = await httpClient.GetAsync(url);
//                    if (!response.IsSuccessStatusCode)
//                        return new List<string>();

//                    var json = await response.Content.ReadAsStringAsync();
//                    using var doc = System.Text.Json.JsonDocument.Parse(json);
//                    var results = doc.RootElement.GetProperty("results");
//                    var recipes = new List<string>();

//                    foreach (var item in results.EnumerateArray())
//                    {
//                        var title = item.GetProperty("title").GetString();
//                        var ingredients = item.TryGetProperty("extendedIngredients", out var ingArr)
//                            ? string.Join(", ", ingArr.EnumerateArray().Select(i => i.GetProperty("original").GetString()))
//                            : "";
//                        var instructions = item.TryGetProperty("instructions", out var instr) ? instr.GetString() : "";

//                        recipes.Add($"Title: {title}, Ingredients: {ingredients}, Instructions: {instructions}");
//                    }
//                    return recipes;
//                }
//                catch
//                {
//                    return new List<string>();
//                }
//            }
//        }
//    }
//}

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
        // public ChatHub(OpenAIService openAI, ApplicationDbContext context)
        // {
        //     _openAI = openAI;
        //     _context = context;
        // }

        public async Task SendMessage(string message)
        {
            try
            {
                var lowerMsg = message.Trim().ToLower();
                var greetings = new[] { "hi", "hii", "hello", "good morning", "good afternoon", "good evening", "hey" };
                if (greetings.Any(g => lowerMsg.Equals(g)))
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Hello! How can I help you today? Please ask me anything about recipes or ingredients.");
                    return;
                }
                // ... existing code ...
                var searchTerm = message.Trim().ToLower();
                var dbRecipes = await _context.Recipes
                    .Where(r =>
                        EF.Functions.Like(r.Title.ToLower(), $"%{searchTerm}%") ||
                        EF.Functions.Like(r.Ingredients.ToLower(), $"%{searchTerm}%")
                    )
                    .Select(r => new { r.Id, r.Title, r.Ingredients, r.Instructions })
                    .ToListAsync();
                // ... existing code ...
                // Case-insensitive search for recipes in the database
                //var searchTerm = message.Trim().ToLower();
                //var dbRecipes = await _context.Recipes
                //    .Where(r =>
                //        r.Title.ToLower().Contains(searchTerm) ||
                //        r.Ingredients.ToLower().Contains(searchTerm)
                //    )
                //    .Select(r => new { r.Id, r.Title, r.Ingredients, r.Instructions })
                //    .ToListAsync();

                var spoonacularRecipes = await GetSpoonacularRecipesAsync(message);

                if (lowerMsg.Contains("suggest") || lowerMsg.Contains("recipe"))
                {
                    var dbTitles = dbRecipes.Select(r => $"- {r.Title} (View: /Recipe/Details/{r.Id})");
                    var spoonTitles = spoonacularRecipes.Select(r =>
                    {
                        var titleStart = r.IndexOf("Title: ") + 7;
                        var titleEnd = r.IndexOf(",", titleStart);
                        var title = (titleStart >= 0 && titleEnd > titleStart) ? r.Substring(titleStart, titleEnd - titleStart) : r;
                        return $"- {title}";
                    });

                    var allTitles = dbTitles.Concat(spoonTitles).ToList();

                    if (!allTitles.Any())
                    {
                        await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Sorry, I couldn't find any recipes matching your request from either source.");
                        return;
                    }

                    var reply = "Here are some recipes I found:\n" + string.Join("\n", allTitles);
                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
                    return;
                }

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
            var apiKey = "b535a4c67f554ae5bb0479ee64a3ac94"; // Replace with your actual key
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
                        var title = item.GetProperty("title").GetString();
                        var ingredients = item.TryGetProperty("extendedIngredients", out var ingArr)
                            ? string.Join(", ", ingArr.EnumerateArray().Select(i => i.GetProperty("original").GetString()))
                            : "";
                        var instructions = item.TryGetProperty("instructions", out var instr) ? instr.GetString() : "";

                        recipes.Add($"Title: {title}, Ingredients: {ingredients}, Instructions: {instructions}");
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
