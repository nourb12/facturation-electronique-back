using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParametresFiscaux1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParametresFiscaux",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Libelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Valeur = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Signe = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OrdreCalcul = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Utilisation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InclureRetenueSource = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DocumentsCibles = table.Column<string>(type: "text", nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametresFiscaux", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParametresFiscaux_EntrepriseId",
                table: "ParametresFiscaux",
                column: "EntrepriseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParametresFiscaux");
        }
    }
}
