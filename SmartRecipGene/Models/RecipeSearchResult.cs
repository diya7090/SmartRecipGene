namespace SmartRecipGene.Models
{
    public class RecipeSearchResult
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Image { get; set; }
        public int ReadyInMinutes { get; set; }
        public string DietType { get; set; }
        public string Cuisine { get; set; }
        public string Source { get; set; }
    }

    public class SpoonacularSearchResponse
    {
        public List<RecipeSearchResult> Results { get; set; }
        public int Offset { get; set; }
        public int Number { get; set; }
        public int TotalResults { get; set; }
    }
}