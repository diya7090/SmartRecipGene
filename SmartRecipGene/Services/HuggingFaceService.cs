using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SmartRecipGene.Services
{
    public class HuggingFaceService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public HuggingFaceService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["HuggingFace:ApiKey"];
        }

        public async Task<string> GetResponse(string userMessage, string recipeData)
        {
            // Simple greeting logic
            var lowerMsg = userMessage.Trim().ToLower();
            if (lowerMsg == "hi" || lowerMsg == "hii" || lowerMsg == "hello")
            {
                return "Hello! How can I help you today? Please ask me anything about recipes or ingredients.";
            }

            // Otherwise, use Hugging Face model
            var prompt = $"User: {userMessage}\n\nHere are some recipes:\n{recipeData}\n\nBot:";
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api-inference.huggingface.co/models/google/flan-t5-large");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(new { inputs = prompt }), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return $"Error: {response.StatusCode} - {responseJson}";
            }

            // Parse the generated_text from the response
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var generatedText = doc.RootElement[0].GetProperty("generated_text").GetString();
                    return generatedText ?? "No response generated.";
                }
                return "Unexpected response format.";
            }
            catch
            {
                return "Failed to parse response: " + responseJson;
            }
        }
    }
}