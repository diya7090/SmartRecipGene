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
        
        public DbSet<Order> Orders { get; set; }

        public DbSet<OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //modelBuilder.Entity<ApplicationUser>()
            //   .ToTable("AspNetUsers");

             modelBuilder.Entity<ShoppingListItem>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.DateAdded)
                    .HasDefaultValueSql("GETDATE()");
                entity.Property(s => s.UserId)
                    .HasColumnType("nvarchar(450)");
                entity.HasOne(r => r.User)
                    .WithMany()
                    .HasForeignKey(s => s.UserId)
                    .OnDelete(DeleteBehavior.NoAction);
                // RecipeId is not a foreign key since we use external API
                entity.Property(s => s.RecipeId)
                    .IsRequired();
            });

            modelBuilder.Entity<Recipe>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.PricePerServing)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.HasIndex(r => r.Title);
            });

    

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

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(o => o.Id);
                entity.Property(o => o.OrderNumber)
                    .IsRequired();
                entity.Property(o => o.TotalAmount)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.Property(o => o.Status)
                    .IsRequired();
                entity.Property(o => o.DeliveryAddress)
                    .IsRequired();
                entity.Property(o => o.UserId)
                    .HasColumnType("nvarchar(450)")
                    .IsRequired();
                entity.HasOne(o => o.User)
                    .WithMany()
                    .HasForeignKey(o => o.UserId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(oi => oi.Id);
                entity.Property(oi => oi.RecipeId)
                    .IsRequired();
                entity.Property(oi => oi.PricePerServings)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.Property(oi => oi.TotalPrice)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.HasOne(oi => oi.Order)
                    .WithMany(o => o.OrderItems)
                    .HasForeignKey(oi => oi.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        
        }

    }
}

