//using Microsoft.AspNetCore.Identity;
//using System.Collections.Generic;
//using System.ComponentModel.DataAnnotations;

//namespace SmartRecipGene.Models
//{
//    public class ApplicationUser : IdentityUser
//    {
//        [PersonalData]
//        [StringLength(100)]
//        public string Name { get; set; }


//        [PersonalData]
//        [StringLength(1000)]
//        public string Preferences { get; set; } = "[]";
//        public List<Recipe> SavedRecipes { get; set; } // User's saved recipes
//        public ICollection<UserActivity> Activities { get; set; } = new List<UserActivity>(); // ✅ FIXED
//    }
//}


using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartRecipGene.Models
{
    public class ApplicationUser : IdentityUser
    {
        public ApplicationUser()
        {
            SavedRecipes = new List<Recipe>();
            Activities = new List<UserActivity>();
            FavoriteRecipes = new List<FavoriteRecipe>();
            Reviews = new List<Review>();
        }

        [PersonalData]
        [StringLength(100)]
        public string Name { get; set; }

        [PersonalData]
        [StringLength(1000)]
        public string Preferences { get; set; } = "[]";

        public virtual ICollection<Recipe> SavedRecipes { get; set; }
        public virtual ICollection<UserActivity> Activities { get; set; }
        public virtual ICollection<FavoriteRecipe> FavoriteRecipes { get; set; }
        public virtual ICollection<Review> Reviews { get; set; }
    }
}