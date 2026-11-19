using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    /// <summary>
    /// Paie : contribution sociale de solidarité sur les bulletins et situation familiale des employés
    /// (déductions IRPP pour charges de famille). Valeurs existantes : 0 / aucune charge.
    /// </summary>
    [DbContext(typeof(ContextBaseDeDonnees))]
    [Migration("20261117120000_AddCssSituationFamilialePaie")]
    public partial class AddCssSituationFamilialePaie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(name: "Css", table: "BulletinsPaie", type: "numeric(15,3)", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<bool>(name: "ChefDeFamille", table: "EmployesPaie", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<int>(name: "EnfantsACharge", table: "EmployesPaie", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "EnfantsEtudiants", table: "EmployesPaie", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "EnfantsHandicapes", table: "EmployesPaie", type: "integer", nullable: false, defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Css", table: "BulletinsPaie");
            migrationBuilder.DropColumn(name: "ChefDeFamille", table: "EmployesPaie");
            migrationBuilder.DropColumn(name: "EnfantsACharge", table: "EmployesPaie");
            migrationBuilder.DropColumn(name: "EnfantsEtudiants", table: "EmployesPaie");
            migrationBuilder.DropColumn(name: "EnfantsHandicapes", table: "EmployesPaie");
        }
    }
}
