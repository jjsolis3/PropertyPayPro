using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PropertyPayPro.Migrations
{
    /// <inheritdoc />
    public partial class EmailAndPdfArchiveFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReceiveReceipts",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiveReminders",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EmailLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ToAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LeaseId = table.Column<int>(type: "integer", nullable: true),
                    RentPaymentId = table.Column<int>(type: "integer", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    LeaseId = table.Column<int>(type: "integer", nullable: true),
                    RentalChargeId = table.Column<int>(type: "integer", nullable: true),
                    RentPaymentId = table.Column<int>(type: "integer", nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneratedDocuments_Leases_LeaseId",
                        column: x => x.LeaseId,
                        principalTable: "Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GeneratedDocuments_RentPayments_RentPaymentId",
                        column: x => x.RentPaymentId,
                        principalTable: "RentPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GeneratedDocuments_RentalCharges_RentalChargeId",
                        column: x => x.RentalChargeId,
                        principalTable: "RentalCharges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocuments_LeaseId",
                table: "GeneratedDocuments",
                column: "LeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocuments_RentalChargeId",
                table: "GeneratedDocuments",
                column: "RentalChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocuments_RentPaymentId",
                table: "GeneratedDocuments",
                column: "RentPaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailLogs");

            migrationBuilder.DropTable(
                name: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "ReceiveReceipts",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ReceiveReminders",
                table: "Tenants");
        }
    }
}
