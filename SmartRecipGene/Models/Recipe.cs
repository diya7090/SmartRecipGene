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
       
        public string? Equipment { get; set; }
        // public List<string>? StepByStepImages { get; set; }
        public decimal? EstimatedCost { get; set; } // Estimated total cost of the recipe
        public ICollection<Review> Reviews { get; set; }
        public ICollection<ShoppingList> ShoppingLists { get; set; }

        // Kit-related properties
        public bool IsKitAvailable { get; set; } = false;
        public decimal? KitPrice { get; set; }
        public int? KitServingSize { get; set; }
        public string? KitDescription { get; set; }
        public string? KitImageUrl { get; set; }
        public bool IsKitInStock { get; set; } = true;

        public Recipe()
        {
            Reviews = new HashSet<Review>();
            ShoppingLists = new HashSet<ShoppingList>();
        }

    }
}
