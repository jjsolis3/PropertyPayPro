using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyPayPro.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoApplyLateFees",
                table: "AppSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AutoBillingDayOfMonth",
                table: "AppSettings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "AutoEmailAdminSummary",
                table: "AppSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoEmailStatements",
                table: "AppSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoGenerateCharges",
                table: "AppSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoApplyLateFees",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AutoBillingDayOfMonth",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AutoEmailAdminSummary",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AutoEmailStatements",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AutoGenerateCharges",
                table: "AppSettings");
        }
    }
}
