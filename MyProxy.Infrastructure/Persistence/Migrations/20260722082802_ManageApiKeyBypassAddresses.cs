using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyProxy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManageApiKeyBypassAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_key_bypass_addresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_key_bypass_addresses", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "api_key_bypass_addresses",
                columns: new[] { "id", "address", "description", "is_enabled", "created_at" },
                values: new object[]
                {
                    new Guid("672e2dcf-8ee4-4cd2-a240-a727c6d2d69c"),
                    "172.29.187.90",
                    "ATFM server",
                    true,
                    new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero)
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_key_bypass_addresses_address",
                table: "api_key_bypass_addresses",
                column: "address",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_key_bypass_addresses");
        }
    }
}
