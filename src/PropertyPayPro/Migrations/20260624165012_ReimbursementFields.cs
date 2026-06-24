using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyPayPro.Migrations
{
    /// <inheritdoc />
    public partial class ReimbursementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReimbursedAmount",
                table: "PropertyExpenses",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReimbursedOn",
                table: "PropertyExpenses",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReimbursedAmount",
                table: "PropertyExpenses");

            migrationBuilder.DropColumn(
                name: "ReimbursedOn",
                table: "PropertyExpenses");
        }
    }
}
