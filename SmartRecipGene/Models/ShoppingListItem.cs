// using System.ComponentModel.DataAnnotations;

// namespace SmartRecipGene.Models
// {
//     public class ShoppingListItem
//     {
//         public int Id { get; set; }
//         public string UserId { get; set; }
//         public int RecipeId { get; set; }
//         public DateTime DateAdded { get; set; } = DateTime.Now;
//     }


// }

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartRecipGene.Models
{
    public class ShoppingListItem
    {
      
       
            public int Id { get; set; }

            [Required]
            public string UserId { get; set; }

            [Required]
            public int RecipeId { get; set; }

           public int Servings { get; set; } = 1;

           public bool Purchased { get; set; } = false;

           public DateTime DateAdded { get; set; } = DateTime.Now;

   
          
             public ApplicationUser User { get; set; }
        }
    }

       

    