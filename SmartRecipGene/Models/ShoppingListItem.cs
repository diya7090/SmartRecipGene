using System.ComponentModel.DataAnnotations;

namespace SmartRecipGene.Models
{
    public class ShoppingListItem
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int RecipeId { get; set; }
        public DateTime DateAdded { get; set; } = DateTime.Now;
    }
    //public class ShoppingListItem
    //{
    //    public int Id { get; set; }  // Primary key
    //    public string UserId { get; set; }=string.Empty;

    //    [Required]
    //    public string Ingredient { get; set; }=string.Empty;

    //    public bool Purchased { get; set; } = false;
        
    //}

}

       

    