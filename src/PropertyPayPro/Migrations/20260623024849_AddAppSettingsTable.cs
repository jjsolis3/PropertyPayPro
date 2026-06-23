using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyPayPro.Migrations
{
    /// <inheritdoc />
    public partial class AddAppSettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    AppName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PrimaryColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccentColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LogoStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LogoSmallStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FromEmailOverride = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    FromNameOverride = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DefaultRentDueDay = table.Column<int>(type: "integer", nullable: false),
                    DefaultLateFeeGraceDays = table.Column<int>(type: "integer", nullable: false),
                    DefaultLateFeeAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");
        }
    }
}
