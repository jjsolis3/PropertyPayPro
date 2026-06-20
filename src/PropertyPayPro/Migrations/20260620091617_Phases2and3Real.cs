using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PropertyPayPro.Migrations
{
    /// <inheritdoc />
    public partial class Phases2and3Real : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RentalCharges_LeaseId_BillingPeriodStart",
                table: "RentalCharges");

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "RentalCharges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsMonthToMonth",
                table: "Leases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LateFeeAmount",
                table: "Leases",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LateFeeGraceDays",
                table: "Leases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LeaseDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeaseId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UploadedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaseDocuments_Leases_LeaseId",
                        column: x => x.LeaseId,
                        principalTable: "Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Vendor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AmountDue = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PassThroughToTenant = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyExpenses_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropertyId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Vendor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReportedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ResolvedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    PassThroughToTenant = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceTickets_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RentalCharges_LeaseId_BillingPeriodStart_Kind",
                table: "RentalCharges",
                columns: new[] { "LeaseId", "BillingPeriodStart", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaseDocuments_LeaseId",
                table: "LeaseDocuments",
                column: "LeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyExpenses_PropertyId",
                table: "PropertyExpenses",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_PropertyId",
                table: "ServiceTickets",
                column: "PropertyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaseDocuments");

            migrationBuilder.DropTable(
                name: "PropertyExpenses");

            migrationBuilder.DropTable(
                name: "ServiceTickets");

            migrationBuilder.DropIndex(
                name: "IX_RentalCharges_LeaseId_BillingPeriodStart_Kind",
                table: "RentalCharges");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "RentalCharges");

            migrationBuilder.DropColumn(
                name: "IsMonthToMonth",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "LateFeeAmount",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "LateFeeGraceDays",
                table: "Leases");

            migrationBuilder.CreateIndex(
                name: "IX_RentalCharges_LeaseId_BillingPeriodStart",
                table: "RentalCharges",
                columns: new[] { "LeaseId", "BillingPeriodStart" },
                unique: true);
        }
    }
}
