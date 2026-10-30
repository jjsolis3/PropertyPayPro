using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyPayPro.Migrations
{
    /// <inheritdoc />
    public partial class LeaseDocumentSourceAndSendTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceTitle",
                table: "LeaseDocuments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceBody",
                table: "LeaseDocuments",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SentCount",
                table: "LeaseDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSentUtc",
                table: "LeaseDocuments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SourceTitle", table: "LeaseDocuments");
            migrationBuilder.DropColumn(name: "SourceBody", table: "LeaseDocuments");
            migrationBuilder.DropColumn(name: "SentCount", table: "LeaseDocuments");
            migrationBuilder.DropColumn(name: "LastSentUtc", table: "LeaseDocuments");
        }
    }
}
