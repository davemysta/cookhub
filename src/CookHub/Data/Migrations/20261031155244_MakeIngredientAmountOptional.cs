using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeIngredientAmountOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Amount",
                table: "RecipeIngredients",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lines without an amount go back to an empty string, which the old column allowed.
            migrationBuilder.Sql("UPDATE RecipeIngredients SET Amount = N'' WHERE Amount IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "Amount",
                table: "RecipeIngredients",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
