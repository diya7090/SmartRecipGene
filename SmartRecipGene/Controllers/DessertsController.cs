using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace SmartRecipGene.Controllers
{
    [Authorize]

    public class DessertsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string apiKey = "c70f25dffdda4764b04e7a7b67e43a67";

    public DessertsController(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IActionResult> Index()
    {
            //string url = $"https://api.spoonacular.com/recipes/complexSearch?apiKey={apiKey}&type=dessert";
            string url = $"https://api.spoonacular.com/recipes/complexSearch?apiKey={apiKey}&query=dessert&number=100";

            var response = await _httpClient.GetStringAsync(url);
        var jsonResponse = JObject.Parse(response);
        var recipes = jsonResponse["results"] ?? new JArray();

        return View(recipes);
    }
}

}
    //public class DessertsController : Controller
    //{
    //    public IActionResult Index()
    //    {
    //        return View();
    //    }
    //}

