using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;

namespace SmartRecipGene.Models

{

    public class Order

    {

        public int Id { get; set; }

        [Required]

        public string UserId { get; set; }

        [Required]

        public string OrderNumber { get; set; }

        [Required]

        public decimal TotalAmount { get; set; }

        [Required]

        public string Status { get; set; } = "Pending"; // Pending, Processing, Shipped, Delivered
        [Required]
        public int AddressId { get; set; }

        [ForeignKey("AddressId")]
        public Address DeliveryAddress { get; set; }

        public string? TrackingNumber { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public DateTime? DeliveryDate { get; set; }

        // Navigation properties

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public ApplicationUser User { get; set; }


    }

    public class OrderItem

    {

        public int Id { get; set; }

        [Required]

        public int OrderId { get; set; }

        [Required]

        public int RecipeId { get; set; }

        [Required]

        public int Servings { get; set; }

        [Required]

        public decimal PricePerServings { get; set; }

        [Required]

        public decimal TotalPrice { get; set; }

        // Navigation properties

        public Order Order { get; set; }
        public Recipe Recipe { get; set; } // ✅ Add this line


    }

}