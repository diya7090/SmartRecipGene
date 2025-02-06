using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRecipGene.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoriteRecipesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ShoppingListItems",
                table: "ShoppingListItems");

            migrationBuilder.RenameTable(
                name: "ShoppingListItems",
                newName: "ShoppingList");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ShoppingList",
                table: "ShoppingList",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "FavoriteRecipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteRecipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FavoriteRecipes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteRecipes_UserId",
                table: "FavoriteRecipes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FavoriteRecipes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ShoppingList",
                table: "ShoppingList");

            migrationBuilder.RenameTable(
                name: "ShoppingList",
                newName: "ShoppingListItems");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ShoppingListItems",
                table: "ShoppingListItems",
                column: "Id");
        }
    }
}
