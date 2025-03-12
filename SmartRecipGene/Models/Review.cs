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
        [StringLength(1000)]
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ApplicationUser User { get; set; }
        public Recipe Recipe { get; set; }
    }
}
