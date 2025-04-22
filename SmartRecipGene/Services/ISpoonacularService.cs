using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartRecipGene.Services
{
    public interface ISpoonacularService
    {
        Task<string> GetRecipesByIngredientsAsync(string ingredients);
        Task<string> GetRecipeDetailsAsync(int recipeId);
        Task<List<JObject>> GetTopRatedRecipes();
        Task<int> GetTotalRecipesCount();
    }
}
