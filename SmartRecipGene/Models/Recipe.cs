using System.ComponentModel.DataAnnotations.Schema;

namespace SmartRecipGene.Models
{
    public class Recipe
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? ImageUrl { get; set; } // New Field

        public string? Ingredients { get; set; }
        public string? Instructions { get; set; }
        public int CookingTime { get; set; }
        public int PreparationTime { get; set; }
        public int TotalTime => PreparationTime + CookingTime;
        public int Servings { get; set; }
        public string? ServingSize { get; set; }
        public string? DifficultyLevel { get; set; }
       
        public string CusineType { get; set; }
        public string? DietType { get; set; }
        public string? Allergens { get; set; }
        public string? MealType { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePerServing { get; set; }
        public string? Equipment { get; set; }
        // public List<string>? StepByStepImages { get; set; }
        public ICollection<Review> Reviews { get; set; }

        public Recipe()
        {
            Reviews = new HashSet<Review>();
        }

    }
}
