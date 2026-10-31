using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PropertyPayPro.Migrations;

public partial class AddNoticeTemplates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NoticeTemplates",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Scope = table.Column<int>(type: "integer", nullable: false),
                Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                TitleOrSubject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                Body = table.Column<string>(type: "text", nullable: false),
                IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => { table.PrimaryKey("PK_NoticeTemplates", x => x.Id); });

        migrationBuilder.CreateIndex("IX_NoticeTemplates_Scope_Slug", "NoticeTemplates",
            new[] { "Scope", "Slug" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("NoticeTemplates");
    }
}
