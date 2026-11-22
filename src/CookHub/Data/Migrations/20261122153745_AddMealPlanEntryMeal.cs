using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMealPlanEntryMeal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Meal",
                table: "MealPlanEntries",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Meal",
                table: "MealPlanEntries");
        }
    }
}
