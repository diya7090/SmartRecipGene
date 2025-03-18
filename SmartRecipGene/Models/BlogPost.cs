using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartRecipGene.Models
{
    public class BlogPost
    {
        public int Id { get; set; } // Unique identifier
        [Required]
        [StringLength(200)]
        public string Title { get; set; } // Blog title
                [Required]

        public string Content { get; set; } 
         [Required]
        [StringLength(100)]// Full blog content
        public string Author { get; set; } // Author name
        public DateTime CreatedAt { get; set; } = DateTime.Now; 
                [Url]
        public string? ImageUrl { get; set; }
         [StringLength(50)] // Optional blog image
        public string? Category { get; set; } // Category like "Desserts", "Healthy Baking"
        public int Views { get; set; } = 0; // Number of views
        public bool IsPublished { get; set; } = true; 
                [StringLength(500)]
// Published or Draft
        public string? Tags { get; set; } // Optional tags (comma-separated like "chocolate,cake,baking")

       

        public int? RecipeId { get; set; } // Nullable in case a blog post doesn't have a recipe

        [ForeignKey("RecipeId")]
        public Recipe? Recipe { get; set; }

    }



}
