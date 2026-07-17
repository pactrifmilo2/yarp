using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyProxy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditQueryString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "query_string",
                table: "audit_entries",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "query_string",
                table: "audit_entries");
        }
    }
}
