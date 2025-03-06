using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartRecipGene.Models;

namespace SmartRecipGene.Data
{
    public class ApplicationDbContext: IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Review> Reviews { get; set; }

        public DbSet<ShoppingListItem> ShoppingList { get; set; }

        public DbSet<FavoriteRecipe> FavoriteRecipes { get; set; } // Add this line

        public DbSet<BlogPost> BlogPosts { get; set; }

        public DbSet<Recipe> Recipes { get; set; }

        public DbSet<UserActivity> UserActivities { get; set; }

        //public DbSet<ShoppingListItem> ShoppingList { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ShoppingListItem>()
                .HasKey(s => s.Id);  // Explicitly setting the primary key
        }

    }
}
