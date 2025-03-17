//using Microsoft.AspNetCore.Identity;
//using System.ComponentModel.DataAnnotations.Schema;
//using System.ComponentModel.DataAnnotations;

//namespace SmartRecipGene.Models
//{
//    public class FavoriteRecipe
//    {
//        [Key]
//        public int Id { get; set; }

//        [Required]
//        public string RecipeId { get; set; } // Recipe ID from Spoonacular API

//        [Required]
//        public string Title { get; set; } // Recipe Name

//        public string ImageUrl { get; set; } // Recipe Image URL

//        [Required]
//        public string UserId { get; set; } // Link to the User

//        [ForeignKey("UserId")]
//        public ApplicationUser User { get; set; } // Navigation Property

//        public bool IsExternalRecipe { get; set; }

//        [ForeignKey("LocalRecipeId")]
//        public Recipe LocalRecipe { get; set; }

//        public int? LocalRecipeId { get; set; }

//        public DateTime DateAdded { get; set; } = DateTime.UtcNow;
//    }
//}

using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartRecipGene.Models
{
    public class FavoriteRecipe
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public string ImageUrl { get; set; }

        [Required]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        public int RecipeId { get; set; }

        public bool IsExternalRecipe { get; set; }

        public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    }
}