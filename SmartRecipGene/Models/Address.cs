using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace SmartRecipGene.Models
{
    public class Address
    { 
        public int Id { get; set; }

       
        public string UserId { get; set; }

       

        [Required]
        [Phone]
        public string PhoneNumber { get; set; }

        [Required]
        [StringLength(200)]
        public string StreetAddress { get; set; }



        [Required]
        [StringLength(100)]
        public string City { get; set; }

        [Required]
        [StringLength(100)]
        public string State { get; set; }

        [Required]
        [StringLength(20)]
        public string PostalCode { get; set; }

       

        [StringLength(500)]
        public string Notes { get; set; }

        // Navigation property
        [BindNever]
        public ApplicationUser User { get; set; }
    }
}