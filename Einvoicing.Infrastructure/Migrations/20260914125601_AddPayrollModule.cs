using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BulletinsPaie",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Periode = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Statut = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Brut = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    CnssSalarie = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    Irpp = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    Avances = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    NetAPayer = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    DatePaiement = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferencePaiement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulletinsPaie", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployesPaie",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Matricule = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Nom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Poste = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Departement = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Cin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NumeroCnss = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Rib = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SalaireBase = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    PrimeMensuelle = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    AvantageMensuel = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployesPaie", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BulletinsPaie_EntrepriseId_EmployeId_Periode",
                table: "BulletinsPaie",
                columns: new[] { "EntrepriseId", "EmployeId", "Periode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BulletinsPaie_EntrepriseId_Periode",
                table: "BulletinsPaie",
                columns: new[] { "EntrepriseId", "Periode" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployesPaie_EntrepriseId_Matricule",
                table: "EmployesPaie",
                columns: new[] { "EntrepriseId", "Matricule" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BulletinsPaie");

            migrationBuilder.DropTable(
                name: "EmployesPaie");
        }
    }
}
