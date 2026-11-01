using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyPayPro.Migrations;

public partial class AddPublicBaseUrl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PublicBaseUrl",
            table: "AppSettings",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PublicBaseUrl", table: "AppSettings");
    }
}
