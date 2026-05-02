using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoRequestEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemoRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PreferredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PreferredTime = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemoRequests_CreatedAt",
                table: "DemoRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DemoRequests_Email",
                table: "DemoRequests",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_DemoRequests_Status",
                table: "DemoRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemoRequests");
        }
    }
}
