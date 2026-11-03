using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyPayPro.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderSwitches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoInspectionReminders",
                table: "AppSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoLateRentReminders",
                table: "AppSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoLeaseRenewalReminders",
                table: "AppSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoInspectionReminders",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AutoLateRentReminders",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AutoLeaseRenewalReminders",
                table: "AppSettings");
        }
    }
}
