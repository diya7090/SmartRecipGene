using System.ComponentModel.DataAnnotations.Schema;

namespace SmartRecipGene.Models
{
    public class BlogPost
    {
        public int Id { get; set; } // Unique identifier
        public string Title { get; set; } // Blog title
        public string Content { get; set; } // Full blog content
        public string Author { get; set; } // Author name
        public DateTime CreatedAt { get; set; } = DateTime.Now; // Created timestamp
        public string? ImageUrl { get; set; } // Optional blog image
        public string? Category { get; set; } // Category like "Desserts", "Healthy Baking"
        public int Views { get; set; } = 0; // Number of views
        public bool IsPublished { get; set; } = true; // Published or Draft
        public string? Tags { get; set; } // Optional tags (comma-separated like "chocolate,cake,baking")

       

        public int? RecipeId { get; set; } // Nullable in case a blog post doesn't have a recipe

        [ForeignKey("RecipeId")]
        public Recipe? Recipe { get; set; }

    }



}
