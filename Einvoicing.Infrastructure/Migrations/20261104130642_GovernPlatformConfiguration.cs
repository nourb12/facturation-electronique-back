using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GovernPlatformConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ParametresPlateforme",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiePar",
                table: "ParametresPlateforme",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TypeValeur",
                table: "ParametresPlateforme",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Verrouille",
                table: "ParametresPlateforme",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "HistoriqueParametresPlateforme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParametreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AncienneValeur = table.Column<string>(type: "text", nullable: false),
                    NouvelleValeur = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Motif = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ModifiePar = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoriqueParametresPlateforme", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoriqueParametresPlateforme_ParametresPlateforme_Paramet~",
                        column: x => x.ParametreId,
                        principalTable: "ParametresPlateforme",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoriqueParametresPlateforme_ModifiePar",
                table: "HistoriqueParametresPlateforme",
                column: "ModifiePar");

            migrationBuilder.CreateIndex(
                name: "IX_HistoriqueParametresPlateforme_ParametreId_ModifieLe",
                table: "HistoriqueParametresPlateforme",
                columns: new[] { "ParametreId", "ModifieLe" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoriqueParametresPlateforme");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ParametresPlateforme");

            migrationBuilder.DropColumn(
                name: "ModifiePar",
                table: "ParametresPlateforme");

            migrationBuilder.DropColumn(
                name: "TypeValeur",
                table: "ParametresPlateforme");

            migrationBuilder.DropColumn(
                name: "Verrouille",
                table: "ParametresPlateforme");
        }
    }
}
