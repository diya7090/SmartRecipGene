using System.ComponentModel.DataAnnotations;

namespace SmartRecipGene.Models
{
    public class ShoppingListItem
    {
        public int Id { get; set; }  // Primary key
        public string UserId { get; set; }=string.Empty;

        [Required]
        public string Ingredient { get; set; }=string.Empty;

        public bool Purchased { get; set; } = false;
        //[DataType(DataType.DateTime)]
        //public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        //[DataType(DataType.DateTime)]
        //public DateTime? PurchaseDate { get; set; }

        //public int? Quantity { get; set; }

        //[StringLength(500)]
        //public string? Notes { get; set; }
    }

}

       

    