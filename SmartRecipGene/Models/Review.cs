using System.ComponentModel.DataAnnotations;

namespace SmartRecipGene.Models
{
    public class Review
    {
        public int Id { get; set; }
        public int RecipeId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [Required]
        [StringLength(500)]
        public string Comment { get; set; } = string.Empty;

       
    }
}
