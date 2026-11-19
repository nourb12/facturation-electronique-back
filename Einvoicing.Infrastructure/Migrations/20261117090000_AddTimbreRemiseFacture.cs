using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    /// <summary>
    /// Ajoute la remise globale et le droit de timbre aux factures.
    /// Les factures existantes gardent 0 : leurs montants enregistrés ne changent pas.
    /// </summary>
    [DbContext(typeof(ContextBaseDeDonnees))]
    [Migration("20261117090000_AddTimbreRemiseFacture")]
    public partial class AddTimbreRemiseFacture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RemiseGlobale",
                table: "Factures",
                type: "numeric(15,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TimbreFiscal",
                table: "Factures",
                type: "numeric(15,3)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RemiseGlobale", table: "Factures");
            migrationBuilder.DropColumn(name: "TimbreFiscal", table: "Factures");
        }
    }
}
