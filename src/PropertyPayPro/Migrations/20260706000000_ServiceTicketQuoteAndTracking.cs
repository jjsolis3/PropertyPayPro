using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyPayPro.Migrations
{
    /// <inheritdoc />
    public partial class ServiceTicketQuoteAndTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "ServiceTickets",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "VendorPhone",
                table: "ServiceTickets",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ScheduledFor",
                table: "ServiceTickets",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "QuotedOn",
                table: "ServiceTickets",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuotedAmount",
                table: "ServiceTickets",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WarrantyExpiresOn",
                table: "ServiceTickets",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Priority", table: "ServiceTickets");
            migrationBuilder.DropColumn(name: "VendorPhone", table: "ServiceTickets");
            migrationBuilder.DropColumn(name: "ScheduledFor", table: "ServiceTickets");
            migrationBuilder.DropColumn(name: "QuotedOn", table: "ServiceTickets");
            migrationBuilder.DropColumn(name: "QuotedAmount", table: "ServiceTickets");
            migrationBuilder.DropColumn(name: "WarrantyExpiresOn", table: "ServiceTickets");
        }
    }
}
