using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniGroup.CRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCallClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MainCategory",
                table: "Calls",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubCategory",
                table: "Calls",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Calls_MainCategory",
                table: "Calls",
                column: "MainCategory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Calls_MainCategory",
                table: "Calls");

            migrationBuilder.DropColumn(
                name: "MainCategory",
                table: "Calls");

            migrationBuilder.DropColumn(
                name: "SubCategory",
                table: "Calls");
        }
    }
}
