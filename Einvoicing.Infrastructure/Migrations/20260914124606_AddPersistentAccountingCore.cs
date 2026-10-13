using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPersistentAccountingCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActifsComptables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Libelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Famille = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Compte = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    TauxAmortissement = table.Column<decimal>(type: "numeric(5,3)", nullable: false),
                    ValeurAcquisition = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    DateAcquisition = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Statut = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActifsComptables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComptesComptables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    Intitule = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Classe = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Nature = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Usage = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComptesComptables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EcrituresComptables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Journal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Piece = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Compte = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Tiers = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Debit = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    Statut = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreePar = table.Column<Guid>(type: "uuid", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcrituresComptables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExercicesComptables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Annee = table.Column<int>(type: "integer", nullable: false),
                    DateDebut = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateFin = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Statut = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClotureLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExercicesComptables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FacturesFournisseur",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FournisseurId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreePar = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Statut = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DateFacture = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateEcheance = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalHt = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    TotalTva = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    TotalTtc = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    TauxRetenue = table.Column<decimal>(type: "numeric(5,3)", nullable: false),
                    MontantRetenue = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    NetAPayer = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    MontantPaye = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    LignesJson = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TransactionOrigineId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturesFournisseur", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaiementsFournisseur",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactureFournisseurId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnregistrePar = table.Column<Guid>(type: "uuid", nullable: false),
                    Montant = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DatePaiement = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaiementsFournisseur", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActifsComptables_EntrepriseId",
                table: "ActifsComptables",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_ComptesComptables_EntrepriseId_Numero",
                table: "ComptesComptables",
                columns: new[] { "EntrepriseId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EcrituresComptables_EntrepriseId_Date",
                table: "EcrituresComptables",
                columns: new[] { "EntrepriseId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_EcrituresComptables_EntrepriseId_GroupeId",
                table: "EcrituresComptables",
                columns: new[] { "EntrepriseId", "GroupeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExercicesComptables_EntrepriseId_Annee",
                table: "ExercicesComptables",
                columns: new[] { "EntrepriseId", "Annee" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacturesFournisseur_EntrepriseId_Numero",
                table: "FacturesFournisseur",
                columns: new[] { "EntrepriseId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacturesFournisseur_FournisseurId",
                table: "FacturesFournisseur",
                column: "FournisseurId");

            migrationBuilder.CreateIndex(
                name: "IX_PaiementsFournisseur_EntrepriseId",
                table: "PaiementsFournisseur",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_PaiementsFournisseur_FactureFournisseurId",
                table: "PaiementsFournisseur",
                column: "FactureFournisseurId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActifsComptables");

            migrationBuilder.DropTable(
                name: "ComptesComptables");

            migrationBuilder.DropTable(
                name: "EcrituresComptables");

            migrationBuilder.DropTable(
                name: "ExercicesComptables");

            migrationBuilder.DropTable(
                name: "FacturesFournisseur");

            migrationBuilder.DropTable(
                name: "PaiementsFournisseur");
        }
    }
}
