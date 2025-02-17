using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRecipGene.Migrations
{
    /// <inheritdoc />
    public partial class AddFkInBlogPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Calories",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "CookingTime",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "CuisineType",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "DifficultyLevel",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "Ingredients",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "IsVegetarian",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "Servings",
                table: "BlogPosts");

            migrationBuilder.AddColumn<int>(
                name: "RecipeId",
                table: "BlogPosts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_RecipeId",
                table: "BlogPosts",
                column: "RecipeId");

            migrationBuilder.AddForeignKey(
                name: "FK_BlogPosts_Recipes_RecipeId",
                table: "BlogPosts",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlogPosts_Recipes_RecipeId",
                table: "BlogPosts");

            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_RecipeId",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "RecipeId",
                table: "BlogPosts");

            migrationBuilder.AddColumn<int>(
                name: "Calories",
                table: "BlogPosts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CookingTime",
                table: "BlogPosts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CuisineType",
                table: "BlogPosts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DifficultyLevel",
                table: "BlogPosts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ingredients",
                table: "BlogPosts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "BlogPosts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVegetarian",
                table: "BlogPosts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Servings",
                table: "BlogPosts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
