using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    [DbContext(typeof(ContextBaseDeDonnees))]
    [Migration("20260512210000_AddFactureRetenueSource")]
    public partial class AddFactureRetenueSource : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "AppliquerRS", table: "Factures", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "CodeRS", table: "Factures", type: "character varying(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "TauxRS", table: "Factures", type: "numeric(5,3)", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(name: "BaseRS", table: "Factures", type: "numeric(15,3)", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(name: "MontantRS", table: "Factures", type: "numeric(15,3)", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(name: "NetAPayer", table: "Factures", type: "numeric(15,3)", nullable: false, defaultValue: 0m);
            migrationBuilder.Sql("UPDATE \"Factures\" SET \"NetAPayer\" = \"TotalTtc\" WHERE \"NetAPayer\" = 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AppliquerRS", table: "Factures");
            migrationBuilder.DropColumn(name: "CodeRS", table: "Factures");
            migrationBuilder.DropColumn(name: "TauxRS", table: "Factures");
            migrationBuilder.DropColumn(name: "BaseRS", table: "Factures");
            migrationBuilder.DropColumn(name: "MontantRS", table: "Factures");
            migrationBuilder.DropColumn(name: "NetAPayer", table: "Factures");
        }
    }
}