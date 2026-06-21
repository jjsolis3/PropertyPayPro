using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyPayPro.Migrations
{
    /// <inheritdoc />
    public partial class MultiTenantLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
	{
	    // 1. Create the new junction table first so we can copy data into it.
	    migrationBuilder.CreateTable(
	        name: "LeaseTenants",
	        columns: table => new
	        {
	            LeasesId = table.Column<int>(type: "integer", nullable: false),
	            TenantsId = table.Column<int>(type: "integer", nullable: false)
	        },
	        constraints: table =>
	        {
	            table.PrimaryKey("PK_LeaseTenants", x => new { x.LeasesId, x.TenantsId });
	            table.ForeignKey(
	                name: "FK_LeaseTenants_Leases_LeasesId",
	                column: x => x.LeasesId,
	                principalTable: "Leases",
	                principalColumn: "Id",
	                onDelete: ReferentialAction.Cascade);
	            table.ForeignKey(
	                name: "FK_LeaseTenants_Tenants_TenantsId",
	                column: x => x.TenantsId,
	                principalTable: "Tenants",
	                principalColumn: "Id",
	                onDelete: ReferentialAction.Cascade);
	        });

	    migrationBuilder.CreateIndex(
	        name: "IX_LeaseTenants_TenantsId",
	        table: "LeaseTenants",
	        column: "TenantsId");

	    // 2. Preserve existing single-tenant assignments into the new junction.
	    migrationBuilder.Sql(@"
	        INSERT INTO ""LeaseTenants"" (""LeasesId"", ""TenantsId"")
	        SELECT ""Id"", ""TenantId""
	        FROM ""Leases""
	        WHERE ""TenantId"" IS NOT NULL;
	    ");

	    // 3. Now safe to drop the old FK / index / column.
	    migrationBuilder.DropForeignKey(
	        name: "FK_Leases_Tenants_TenantId",
	        table: "Leases");

	    migrationBuilder.DropIndex(
	        name: "IX_Leases_TenantId",
	        table: "Leases");

	    migrationBuilder.DropColumn(
	        name: "TenantId",
	        table: "Leases");
	}

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaseTenants");

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Leases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Leases_TenantId",
                table: "Leases",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Leases_Tenants_TenantId",
                table: "Leases",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
