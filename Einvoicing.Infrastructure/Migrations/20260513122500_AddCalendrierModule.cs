using System;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    [DbContext(typeof(ContextBaseDeDonnees))]
    [Migration("20260513122500_AddCalendrierModule")]
    public partial class AddCalendrierModule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendrierEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StartTime = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    EndTime = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    LinkedClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LinkedAmount = table.Column<decimal>(type: "numeric(15,3)", nullable: true),
                    ZoomLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReminderMinutes = table.Column<int>(type: "integer", nullable: true),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendrierEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendrierTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Done = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendrierTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendrierEvents_EntrepriseId",
                table: "CalendrierEvents",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendrierEvents_EntrepriseId_Date",
                table: "CalendrierEvents",
                columns: new[] { "EntrepriseId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendrierTasks_EntrepriseId",
                table: "CalendrierTasks",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendrierTasks_EntrepriseId_Date",
                table: "CalendrierTasks",
                columns: new[] { "EntrepriseId", "Date" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CalendrierEvents");
            migrationBuilder.DropTable(name: "CalendrierTasks");
        }
    }
}
