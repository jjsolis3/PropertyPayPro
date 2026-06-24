using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyPayPro.Migrations
{
    /// <inheritdoc />
    public partial class PaidByTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaidByTenantId",
                table: "RentPayments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentPayments_PaidByTenantId",
                table: "RentPayments",
                column: "PaidByTenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_RentPayments_Tenants_PaidByTenantId",
                table: "RentPayments",
                column: "PaidByTenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RentPayments_Tenants_PaidByTenantId",
                table: "RentPayments");

            migrationBuilder.DropIndex(
                name: "IX_RentPayments_PaidByTenantId",
                table: "RentPayments");

            migrationBuilder.DropColumn(
                name: "PaidByTenantId",
                table: "RentPayments");
        }
    }
}
