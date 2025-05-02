


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
        // // private readonly OpenAIService _openAI;
        // private readonly ApplicationDbContext _context;
        // private readonly HuggingFaceService _huggingFaceService;
        // public ChatHub(ApplicationDbContext context, HuggingFaceService huggingFaceService)
        // {
        //     _context = context;
        //     _huggingFaceService = huggingFaceService;
        // }
        // ... existing code ...
        private readonly ApplicationDbContext _context;
        private readonly HuggingFaceService _huggingFaceService;
        private readonly OpenAIService _openAIService; // Add this

        public ChatHub(ApplicationDbContext context, HuggingFaceService huggingFaceService, OpenAIService openAIService)
        {
            _context = context;
            _huggingFaceService = huggingFaceService;
            _openAIService = openAIService; // Add this
        }
        // ... existing code ...
        public async Task SendMessage(string message)
        {
            if (Context.User == null || !Context.User.Identity.IsAuthenticated)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "You must be logged in to chat with the chatbot.");
                return;
            }
            try
            {
                var lowerMsg = message.Trim().ToLower();
                var greetings = new[] { "hi", "hii", "hello", "good morning", "good afternoon", "good evening", "hey", "hyee", "Hello", "Helloo", "oii", };
                if (greetings.Any(g => lowerMsg.Equals(g)))
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Hello! How can I help you today? Please ask me anything about recipes or ingredients.");
                    return;
                }
                if (IsIngredientQuery(lowerMsg))
                {
                    var ingredients = ExtractIngredients(message);
                    if (ingredients.Count > 0)
                    {
                        var ingredientAllRecipes = await _context.Recipes
                            .Select(r => new { r.Id, r.Title, r.Ingredients, r.Instructions })
                            .ToListAsync();

                        var ingredientDbRecipes = ingredientAllRecipes
                            .Where(r => ingredients.All(ing =>
                                (r.Ingredients != null && r.Ingredients.ToLower().Contains(ing.ToLower())) ||
                                (r.Title != null && r.Title.ToLower().Contains(ing.ToLower()))
                            ))
                            .ToList();

                        var ingredientSpoonacularRecipes = await GetSpoonacularRecipesAsync(string.Join(" ", ingredients));

                        var allTitles = new List<string>();
                        allTitles.AddRange(ingredientDbRecipes.Select(r => $"- <a href='/Home/RecipeDetails/{r.Id}'>{r.Title}</a>"));
                        allTitles.AddRange(ingredientSpoonacularRecipes.Select(r =>
                        {
                            var idStart = r.IndexOf("Id: ") + 4;
                            var idEnd = r.IndexOf(",", idStart);
                            var id = (idStart >= 0 && idEnd > idStart) ? r.Substring(idStart, idEnd - idStart) : "";
                            var titleStart = r.IndexOf("Title: ") + 7;
                            var titleEnd = r.IndexOf(",", titleStart);
                            var title = (titleStart >= 0 && titleEnd > titleStart) ? r.Substring(titleStart, titleEnd - titleStart) : r;
                            return $"- <a href='/Home/RecipeDetails/{id}'>{title}</a>";
                        }));

                        if (allTitles.Any())
                        {
                            var reply = "Here are some recipes I found:<br>" + string.Join("<br>", allTitles);
                            await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
                            return;
                        }
                        else
                        {
                            await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Sorry, I couldn't find any recipes matching those ingredients.");
                            return;
                        }
                    }
                }

                // Remove "recipe" from the search term for better matching
                var searchTerm = message.Replace("recipe", "", System.StringComparison.OrdinalIgnoreCase).Trim();

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
                        ? r.Instructions.Substring(0, System.Math.Min(200, r.Instructions.Length)) + (r.Instructions.Length > 200 ? "..." : "")
                        : r.Ingredients.Substring(0, System.Math.Min(200, r.Ingredients.Length)) + (r.Ingredients.Length > 200 ? "..." : "");
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

                    if (allTitles.Any())
                    {
                        var reply = "Here are some recipes I found:<br>" + string.Join("<br>", allTitles);
                        await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
                        return;
                    }
                    else
                    {
                        await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Sorry, I couldn't find any recipes matching your request from either source.");
                        return;
                    }
                }
                // If only Spoonacular recipes found, show them
                else if (dbRecipes.Count == 0 && spoonacularRecipes.Count > 0)
                {
                    var allTitles = spoonacularRecipes.Select(r =>
                    {
                        var idStart = r.IndexOf("Id: ") + 4;
                        var idEnd = r.IndexOf(",", idStart);
                        var id = (idStart >= 0 && idEnd > idStart) ? r.Substring(idStart, idEnd - idStart) : "";
                        var titleStart = r.IndexOf("Title: ") + 7;
                        var titleEnd = r.IndexOf(",", titleStart);
                        var title = (titleStart >= 0 && titleEnd > titleStart) ? r.Substring(titleStart, titleEnd - titleStart) : r;
                        return $"- <a href='/Home/RecipeDetails/{id}'>{title}</a>";
                    }).ToList();

                    var reply = "Here are some recipes I found:<br>" + string.Join("<br>", allTitles);
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

                
            }
            catch (System.Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "Bot", $"Error: {ex.Message}");
            }
        }
        //public async Task SendMessage(string message)
        //{
        //    if (Context.User == null || !Context.User.Identity.IsAuthenticated)
        //    {
        //        await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "You must be logged in to chat with the chatbot.");
        //        return;
        //    }
        //    try
        //    {
        //        var lowerMsg = message.Trim().ToLower();
        //        var greetings = new[] { "hi", "hii", "hello", "good morning", "good afternoon", "good evening", "hey", "hyee", "Hello", "Helloo", "oii", };
        //        if (greetings.Any(g => lowerMsg.Equals(g)))
        //        {
        //            await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Hello! How can I help you today? Please ask me anything about recipes or ingredients.");
        //            return;
        //        }
        //        if (IsIngredientQuery(lowerMsg))
        //        {
        //            var ingredients = ExtractIngredients(message);
        //            if (ingredients.Count > 0)
        //            {
        //                // Use unique variable names for ingredient-based logic
        //                var ingredientAllRecipes = await _context.Recipes
        //                    .Select(r => new { r.Id, r.Title, r.Ingredients, r.Instructions })
        //                    .ToListAsync();

        //                var ingredientDbRecipes = ingredientAllRecipes
        //                    .Where(r => ingredients.All(ing =>
        //                        (r.Ingredients != null && r.Ingredients.ToLower().Contains(ing.ToLower())) ||
        //                        (r.Title != null && r.Title.ToLower().Contains(ing.ToLower()))
        //                    ))
        //                    .ToList();

        //                var ingredientSpoonacularRecipes = await GetSpoonacularRecipesAsync(string.Join(" ", ingredients));

        //                var allTitles = new List<string>();
        //                allTitles.AddRange(ingredientDbRecipes.Select(r => $"- <a href='/Home/RecipeDetails/{r.Id}'>{r.Title}</a>"));
        //                allTitles.AddRange(ingredientSpoonacularRecipes.Select(r =>
        //                {
        //                    var idStart = r.IndexOf("Id: ") + 4;
        //                    var idEnd = r.IndexOf(",", idStart);
        //                    var id = (idStart >= 0 && idEnd > idStart) ? r.Substring(idStart, idEnd - idStart) : "";
        //                    var titleStart = r.IndexOf("Title: ") + 7;
        //                    var titleEnd = r.IndexOf(",", titleStart);
        //                    var title = (titleStart >= 0 && titleEnd > titleStart) ? r.Substring(titleStart, titleEnd - titleStart) : r;
        //                    return $"- <a href='/Home/RecipeDetails/{id}'>{title}</a>";
        //                }));

        //                if (allTitles.Any())
        //                {
        //                    var reply = "Here are ssome recipes I found:<br>" + string.Join("<br>", allTitles);
        //                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
        //                    return;
        //                }
        //                else
        //                {
        //                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Sorry, I couldn't find any recipes matching those ingredients.");
        //                    return;
        //                }
        //            }
        //        }
        //        // --- Ingredient-based suggestion logic ---

        //        // --- END Ingredient-based suggestion logic ---

        //        // Remove "recipe" from the search term for better matching
        //        var searchTerm = message.Replace("recipe", "", System.StringComparison.OrdinalIgnoreCase).Trim();

        //        var dbRecipes = await _context.Recipes
        //            .Where(r =>
        //                EF.Functions.Like(r.Title, $"%{searchTerm}%") ||
        //                EF.Functions.Like(r.Ingredients, $"%{searchTerm}%")
        //            )
        //            .Select(r => new { r.Id, r.Title, r.Ingredients, r.Instructions })
        //            .ToListAsync();

        //        var spoonacularRecipes = await GetSpoonacularRecipesAsync(message);

        //        // Show detailed info and link if exactly one DB recipe matches
        //        if (dbRecipes.Count == 1)
        //        {
        //            var r = dbRecipes.First();
        //            var shortInfo = !string.IsNullOrEmpty(r.Instructions)
        //                ? r.Instructions.Substring(0, System.Math.Min(200, r.Instructions.Length)) + (r.Instructions.Length > 200 ? "..." : "")
        //                : r.Ingredients.Substring(0, System.Math.Min(200, r.Ingredients.Length)) + (r.Ingredients.Length > 200 ? "..." : "");
        //            var link = $"/Home/RecipeDetails/{r.Id}";
        //            var reply = $"<b>{r.Title}</b><br>{shortInfo}<br><a href='{link}'>View Full Recipe</a>";
        //            await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
        //            return;
        //        }
        //        // If multiple DB recipes match, list them with links
        //        else if (dbRecipes.Count > 1)
        //        {
        //            var dbTitles = dbRecipes.Select(r => $"- <a href='/Home/RecipeDetails/{r.Id}'>{r.Title}</a>");
        //            var spoonTitles = spoonacularRecipes.Select(r =>
        //            {
        //                var idStart = r.IndexOf("Id: ") + 4;
        //                var idEnd = r.IndexOf(",", idStart);
        //                var id = (idStart >= 0 && idEnd > idStart) ? r.Substring(idStart, idEnd - idStart) : "";
        //                var titleStart = r.IndexOf("Title: ") + 7;
        //                var titleEnd = r.IndexOf(",", titleStart);
        //                var title = (titleStart >= 0 && titleEnd > titleStart) ? r.Substring(titleStart, titleEnd - titleStart) : r;
        //                return $"- <a href='/Home/RecipeDetails/{id}'>{title}</a>";
        //            });

        //            var allTitles = dbTitles.Concat(spoonTitles).ToList();

        //            if (!allTitles.Any())
        //            {
        //                await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Sorry, I couldn't find any recipes matching your request from either source.");
        //                return;
        //            }

        //            var reply = "Here are some recipes I found:<br>" + string.Join("<br>", allTitles);
        //            await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
        //            return;
        //        }
        //        // If only Spoonacular recipes found, show the first one with a short info
        //        else if (dbRecipes.Count == 0 && spoonacularRecipes.Count > 0)
        //        {
        //            var first = spoonacularRecipes.First();
        //            var idStart = first.IndexOf("Id: ") + 4;
        //            var idEnd = first.IndexOf(",", idStart);
        //            var id = (idStart >= 0 && idEnd > idStart) ? first.Substring(idStart, idEnd - idStart) : "";
        //            var titleStart = first.IndexOf("Title: ") + 7;
        //            var titleEnd = first.IndexOf(",", titleStart);
        //            var title = (titleStart >= 0 && titleEnd > titleStart) ? first.Substring(titleStart, titleEnd - titleStart) : first;
        //            var infoStart = first.IndexOf("Instructions: ");
        //            var info = infoStart > 0 ? first.Substring(infoStart) : first;
        //            var shortInfo = info.Length > 200 ? info.Substring(0, 200) + "..." : info;
        //            var link = $"/Home/RecipeDetails/{id}";
        //            var reply = $"<b>{title}</b><br>{shortInfo}<br><a href='{link}'>View Full Recipe</a>";
        //            await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
        //            return;
        //        }

        //        var allRecipes = dbRecipes
        //            .Select(r => $"Title: {r.Title}, Ingredients: {r.Ingredients}, Instructions: {r.Instructions}")
        //            .Concat(spoonacularRecipes)
        //            .ToList();

        //        if (!allRecipes.Any())
        //        {
        //            await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Sorry, I couldn't find any recipes matching your request from either source.");
        //            return;
        //        }

        //        var recipeData = string.Join("\n\n", allRecipes);
        //        var hfResponse = await _huggingFaceService.GetResponse(message, recipeData);

        //        // Call OpenAI
        //        var openAiResponse = await _openAIService.GetResponse(message, recipeData);

        //        // Combine or choose which to send

        //        var response = await _huggingFaceService.GetResponse(message, recipeData);
        //        await Clients.Caller.SendAsync("ReceiveMessage", "Bot", response);
        //    }
        //    catch (System.Exception ex)
        //    {
        //        await Clients.Caller.SendAsync("ReceiveMessage", "Bot", $"Error: {ex.Message}");
        //    }
        //}

        // Helper: Detect if user is asking for recipes with specific ingredients
        private bool IsIngredientQuery(string message)
        {
            // Looks for "suggest" or "show" or "find" and "recipe" and "with"
            return (message.Contains("recipe") && message.Contains("with"));
        }

        // Helper: Extract ingredients after "with"
        private List<string> ExtractIngredients(string message)
        {
            var lower = message.ToLower();
            int idx = lower.IndexOf("with");
            if (idx >= 0)
            {
                var after = message.Substring(idx + 4).Trim();
                var parts = after.Split(new[] { "and", ",", "&" }, System.StringSplitOptions.RemoveEmptyEntries);
                return parts.Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
            }
            return new List<string>();
        }

        private async Task<List<string>> GetSpoonacularRecipesAsync(string query)
        {
            var apiKey = "1744dc9a3003484da22b0564bc1c4b5d"; // Replace with your actual key
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
//        private readonly ApplicationDbContext _context;
//        private readonly HuggingFaceService _huggingFaceService;
//        private readonly OpenAIService _openAIService;

//        public ChatHub(ApplicationDbContext context, HuggingFaceService huggingFaceService, OpenAIService openAIService)
//        {
//            _context = context;
//            _huggingFaceService = huggingFaceService;
//            _openAIService = openAIService;
//        }

//        public async Task SendMessage(string message)
//        {
//            if (Context.User == null || !Context.User.Identity.IsAuthenticated)
//            {
//                await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "You must be logged in to chat with the chatbot.");
//                return;
//            }
//            try
//            {
//                var lowerMsg = message.Trim().ToLower();
//                var greetings = new[] { "hi", "hii", "hello", "good morning", "good afternoon", "good evening", "hey", "hyee", "Hello", "Helloo", "oii", };
//                if (greetings.Any(g => lowerMsg.Equals(g)))
//                {
//                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Hello! How can I help you today? Please ask me anything about recipes or ingredients.");
//                    return;
//                }

//                if (IsIngredientQuery(lowerMsg))
//                {
//                    var ingredients = ExtractIngredients(message);
//                    if (ingredients.Count > 0)
//                    {
//                        var ingredientAllRecipes = await _context.Recipes
//                            .Select(r => new { r.Id, r.Title, r.Ingredients, r.Instructions })
//                            .ToListAsync();

//                        var ingredientDbRecipes = ingredientAllRecipes
//                            .Where(r => ingredients.All(ing =>
//                                (r.Ingredients != null && r.Ingredients.ToLower().Contains(ing.ToLower())) ||
//                                (r.Title != null && r.Title.ToLower().Contains(ing.ToLower()))
//                            ))
//                            .ToList();

//                        var ingredientSpoonacularRecipes = await GetSpoonacularRecipesAsync(string.Join(" ", ingredients));

//                        var allTitles = new List<string>();
//                        allTitles.AddRange(ingredientDbRecipes.Select(r => $"- <a href='/Home/RecipeDetails/{r.Id}'>{r.Title}</a>"));
//                        allTitles.AddRange(ingredientSpoonacularRecipes.Select(r =>
//                        {
//                            var idStart = r.IndexOf("Id: ") + 4;
//                            var idEnd = r.IndexOf(",", idStart);
//                            var id = (idStart >= 0 && idEnd > idStart) ? r.Substring(idStart, idEnd - idStart) : "";
//                            var titleStart = r.IndexOf("Title: ") + 7;
//                            var titleEnd = r.IndexOf(",", titleStart);
//                            var title = (titleStart >= 0 && titleEnd > titleStart) ? r.Substring(titleStart, titleEnd - titleStart) : r;
//                            return $"- <a href='/Home/RecipeDetails/{id}'>{title}</a>";
//                        }));

//                        if (allTitles.Any())
//                        {
//                            var reply = "Here are ssome recipes I found:<br>" + string.Join("<br>", allTitles);
//                            await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
//                            return;
//                        }
//                        else
//                        {
//                            await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Sorry, I couldn't find any recipes matching those ingredients.");
//                            return;
//                        }
//                    }
//                }

//                // Remove "recipe" from the search term for better matching
//                var searchTerm = message.Replace("recipe", "", System.StringComparison.OrdinalIgnoreCase).Trim();

//                var dbRecipes = await _context.Recipes
//                    .Where(r =>
//                        EF.Functions.Like(r.Title, $"%{searchTerm}%") ||
//                        EF.Functions.Like(r.Ingredients, $"%{searchTerm}%")
//                    )
//                    .Select(r => new { r.Id, r.Title, r.Ingredients, r.Instructions })
//                    .ToListAsync();

//                var spoonacularRecipes = await GetSpoonacularRecipesAsync(message);

//                // Show detailed info and link if exactly one DB recipe matches
//                if (dbRecipes.Count == 1)
//                {
//                    var r = dbRecipes.First();
//                    var shortInfo = !string.IsNullOrEmpty(r.Instructions)
//                        ? r.Instructions.Substring(0, System.Math.Min(200, r.Instructions.Length)) + (r.Instructions.Length > 200 ? "..." : "")
//                        : r.Ingredients.Substring(0, System.Math.Min(200, r.Ingredients.Length)) + (r.Ingredients.Length > 200 ? "..." : "");
//                    var link = $"/Home/RecipeDetails/{r.Id}";
//                    var reply = $"<b>{r.Title}</b><br>{shortInfo}<br><a href='{link}'>View Full Recipe</a>";
//                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
//                    return;
//                }
//                // If multiple DB recipes match, list them with links
//                else if (dbRecipes.Count > 1)
//                {
//                    var dbTitles = dbRecipes.Select(r => $"- <a href='/Home/RecipeDetails/{r.Id}'>{r.Title}</a>");
//                    var spoonTitles = spoonacularRecipes.Select(r =>
//                    {
//                        var idStart = r.IndexOf("Id: ") + 4;
//                        var idEnd = r.IndexOf(",", idStart);
//                        var id = (idStart >= 0 && idEnd > idStart) ? r.Substring(idStart, idEnd - idStart) : "";
//                        var titleStart = r.IndexOf("Title: ") + 7;
//                        var titleEnd = r.IndexOf(",", titleStart);
//                        var title = (titleStart >= 0 && titleEnd > titleStart) ? r.Substring(titleStart, titleEnd - titleStart) : r;
//                        return $"- <a href='/Home/RecipeDetails/{id}'>{title}</a>";
//                    });

//                    var allTitles = dbTitles.Concat(spoonTitles).ToList();

//                    if (!allTitles.Any())
//                    {
//                        await Clients.Caller.SendAsync("ReceiveMessage", "Bot", "Sorry, I couldn't find any recipes matching your request from either source.");
//                        return;
//                    }

//                    var reply = "Here are some recipes I found:<br>" + string.Join("<br>", allTitles);
//                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
//                    return;
//                }
//                // If only Spoonacular recipes found, show the first one with a short info
//                else if (dbRecipes.Count == 0 && spoonacularRecipes.Count > 0)
//                {
//                    var first = spoonacularRecipes.First();
//                    var idStart = first.IndexOf("Id: ") + 4;
//                    var idEnd = first.IndexOf(",", idStart);
//                    var id = (idStart >= 0 && idEnd > idStart) ? first.Substring(idStart, idEnd - idStart) : "";
//                    var titleStart = first.IndexOf("Title: ") + 7;
//                    var titleEnd = first.IndexOf(",", titleStart);
//                    var title = (titleStart >= 0 && titleEnd > titleStart) ? first.Substring(titleStart, titleEnd - titleStart) : first;
//                    var infoStart = first.IndexOf("Instructions: ");
//                    var info = infoStart > 0 ? first.Substring(infoStart) : first;
//                    var shortInfo = info.Length > 200 ? info.Substring(0, 200) + "..." : info;
//                    var link = $"/Home/RecipeDetails/{id}";
//                    var reply = $"<b>{title}</b><br>{shortInfo}<br><a href='{link}'>View Full Recipe</a>";
//                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", reply);
//                    return;
//                }

//                // Combine all recipes for fallback check
//                var allRecipes = dbRecipes
//                    .Select(r => $"Title: {r.Title}, Ingredients: {r.Ingredients}, Instructions: {r.Instructions}")
//                    .Concat(spoonacularRecipes)
//                    .ToList();

//                if (!allRecipes.Any())
//                {
//                    // Prompt the AI to invent a recipe if it doesn't know one
//                    var aiPrompt = $"Invent a healthy weight loss recipe for '{message}'. " +
//                                   $"If you don't know a real recipe, make one up. " +
//                                   $"Include a creative recipe title, a list of ingredients, and step-by-step instructions for making it. " +
//                                   $"The recipe should be plausible and easy to follow.";

//                    string hfResponse, openAiResponse;
//                    try
//                    {
//                        hfResponse = await _huggingFaceService.GetResponse(aiPrompt, "");
//                    }
//                    catch
//                    {
//                        hfResponse = "Sorry, HuggingFace could not generate a recipe at this time.";
//                    }

//                    try
//                    {
//                        openAiResponse = await _openAIService.GetResponse(aiPrompt, "");
//                    }
//                    catch
//                    {
//                        openAiResponse = "Sorry, OpenAI could not generate a recipe at this time.";
//                    }

//                    var combinedResponse = $"<b>AI Recipe Suggestions:</b><br><br><b>HuggingFace:</b><br>{hfResponse}<br><br><b>OpenAI:</b><br>{openAiResponse}";
//                    await Clients.Caller.SendAsync("ReceiveMessage", "Bot", combinedResponse);
//                    return;
//                }
//            }
//            catch (System.Exception ex)
//            {
//                await Clients.Caller.SendAsync("ReceiveMessage", "Bot", $"Error: {ex.Message}");
//            }
//        }

//        // Helper: Detect if user is asking for recipes with specific ingredients
//        private bool IsIngredientQuery(string message)
//        {
//            // Looks for "suggest" or "show" or "find" and "recipe" and "with"
//            return (message.Contains("recipe") && message.Contains("with"));
//        }

//        // Helper: Extract ingredients after "with"
//        private List<string> ExtractIngredients(string message)
//        {
//            var lower = message.ToLower();
//            int idx = lower.IndexOf("with");
//            if (idx >= 0)
//            {
//                var after = message.Substring(idx + 4).Trim();
//                var parts = after.Split(new[] { "and", ",", "&" }, System.StringSplitOptions.RemoveEmptyEntries);
//                return parts.Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
//            }
//            return new List<string>();
//        }

//        private async Task<List<string>> GetSpoonacularRecipesAsync(string query)
//        {
//            var apiKey = "4f23d090497a4cc6942f7f6e1f3ffca4"; // Replace with your actual key
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
//                        var id = item.GetProperty("id").GetInt32();
//                        var title = item.GetProperty("title").GetString();
//                        var ingredients = item.TryGetProperty("extendedIngredients", out var ingArr)
//                            ? string.Join(", ", ingArr.EnumerateArray().Select(i => i.GetProperty("original").GetString()))
//                            : "";
//                        var instructions = item.TryGetProperty("instructions", out var instr) ? instr.GetString() : "";

//                        // Include the id in the string for later parsing
//                        recipes.Add($"Id: {id}, Title: {title}, Ingredients: {ingredients}, Instructions: {instructions}");
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
