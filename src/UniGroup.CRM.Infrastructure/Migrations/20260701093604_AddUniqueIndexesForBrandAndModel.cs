using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniGroup.CRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexesForBrandAndModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceModels_BrandId",
                table: "DeviceModels");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceModels_BrandId_Name",
                table: "DeviceModels",
                columns: new[] { "BrandId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceBrands_Name",
                table: "DeviceBrands",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceModels_BrandId_Name",
                table: "DeviceModels");

            migrationBuilder.DropIndex(
                name: "IX_DeviceBrands_Name",
                table: "DeviceBrands");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceModels_BrandId",
                table: "DeviceModels",
                column: "BrandId");
        }
    }
}
