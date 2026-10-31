using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PropertyPayPro.Migrations;

/// <summary>
/// Adds two append-friendly tables used by the audit log and the
/// background scheduler. Both are indexed on the fields we'll filter
/// by (CreatedUtc / (EntityType,EntityId) for audit, (Status,RunAtUtc)
/// for the scheduler poll loop).
/// </summary>
public partial class AddAuditAndScheduledTasks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuditEntries",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                UserEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                EntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                EntityId = table.Column<int>(type: "integer", nullable: true),
                Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
            },
            constraints: table => { table.PrimaryKey("PK_AuditEntries", x => x.Id); });

        migrationBuilder.CreateIndex("IX_AuditEntries_CreatedUtc", "AuditEntries", "CreatedUtc");
        migrationBuilder.CreateIndex("IX_AuditEntries_EntityType_EntityId", "AuditEntries",
            new[] { "EntityType", "EntityId" });

        migrationBuilder.CreateTable(
            name: "ScheduledTasks",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Kind = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                RunAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                PayloadJson = table.Column<string>(type: "text", nullable: false),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
            },
            constraints: table => { table.PrimaryKey("PK_ScheduledTasks", x => x.Id); });

        migrationBuilder.CreateIndex("IX_ScheduledTasks_Status_RunAtUtc", "ScheduledTasks",
            new[] { "Status", "RunAtUtc" });

        // Store the rendered body on each EmailLog so admins can resend
        // a failed delivery from the EmailLogs admin page.
        migrationBuilder.AddColumn<string>(
            name: "BodyHtml",
            table: "EmailLogs",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("BodyHtml", "EmailLogs");
        migrationBuilder.DropTable("ScheduledTasks");
        migrationBuilder.DropTable("AuditEntries");
    }
}
