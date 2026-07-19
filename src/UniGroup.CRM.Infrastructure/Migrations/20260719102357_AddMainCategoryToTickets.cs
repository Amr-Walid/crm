using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniGroup.CRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMainCategoryToTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MainCategory",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_MainCategory",
                table: "Tickets",
                column: "MainCategory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_MainCategory",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "MainCategory",
                table: "Tickets");
        }
    }
}
