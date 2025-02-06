using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SmartRecipGene.Models
{
    public class FavoriteRecipe
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string RecipeId { get; set; } // Recipe ID from Spoonacular API

        [Required]
        public string Title { get; set; } // Recipe Name

        public string ImageUrl { get; set; } // Recipe Image URL

        [Required]
        public string UserId { get; set; } // Link to the User

        [ForeignKey("UserId")]
        public IdentityUser User { get; set; } // Navigation Property
    }
}
