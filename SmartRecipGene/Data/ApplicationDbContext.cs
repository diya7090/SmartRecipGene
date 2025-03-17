using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartRecipGene.Models;

namespace SmartRecipGene.Data
{
    public class ApplicationDbContext: IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
         : base(options)
        {
        }
        public DbSet<Review> Reviews { get; set; }

        public DbSet<ShoppingListItem> ShoppingList { get; set; }

        public DbSet<FavoriteRecipe> FavoriteRecipes { get; set; } // Add this line

        public DbSet<BlogPost> BlogPosts { get; set; }

        public DbSet<Recipe> Recipes { get; set; }

        public DbSet<UserActivity> UserActivities { get; set; }

        public DbSet<Contact> Contacts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //modelBuilder.Entity<ApplicationUser>()
            //   .ToTable("AspNetUsers");

            modelBuilder.Entity<ShoppingListItem>()
               .HasKey(s => s.Id);

            modelBuilder.Entity<Recipe>()
                .HasIndex(r => r.Title);

            modelBuilder.Entity<BlogPost>()
                .HasIndex(b => b.Title);
            modelBuilder.Entity<FavoriteRecipe>()
               .HasOne(f => f.User)
               .WithMany()
               .HasForeignKey(f => f.UserId)
               .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
            .Property(r => r.CreatedAt)
            .HasDefaultValueSql("GETDATE()");
        }

    }

    }

