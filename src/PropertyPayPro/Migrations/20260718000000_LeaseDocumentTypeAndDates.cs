using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyPayPro.Migrations
{
    /// <inheritdoc />
    public partial class LeaseDocumentTypeAndDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "LeaseDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveDate",
                table: "LeaseDocuments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpiresOn",
                table: "LeaseDocuments",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Type", table: "LeaseDocuments");
            migrationBuilder.DropColumn(name: "EffectiveDate", table: "LeaseDocuments");
            migrationBuilder.DropColumn(name: "ExpiresOn", table: "LeaseDocuments");
        }
    }
}
