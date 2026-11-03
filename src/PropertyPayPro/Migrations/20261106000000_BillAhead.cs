using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyPayPro.Migrations
{
    /// <inheritdoc />
    public partial class BillAhead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AutoBillingDayOfMonth",
                table: "AppSettings",
                newName: "BillIssueDay");

            // The column used to mean "bill this month from day N"; it now means
            // "bill next month on day N of the month before". Start at the 16th.
            migrationBuilder.Sql("UPDATE \"AppSettings\" SET \"BillIssueDay\" = 16;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BillIssueDay",
                table: "AppSettings",
                newName: "AutoBillingDayOfMonth");
        }
    }
}
