using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations;

/// <summary>
/// Adds the platform-wide settings table. Earlier migrations already create
/// the business tables, so this migration must only introduce this new table.
/// </summary>
public partial class AddPlatformSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ParametresPlateforme",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Cle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ValeurJson = table.Column<string>(type: "text", nullable: false),
                ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ParametresPlateforme", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ParametresPlateforme_Cle",
            table: "ParametresPlateforme",
            column: "Cle",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ParametresPlateforme");
    }
}
