using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeStepVideoSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VideoSeconds",
                table: "RecipeSteps",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VideoSeconds",
                table: "RecipeSteps");
        }
    }
}
