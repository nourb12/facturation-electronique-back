using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    
    public partial class module3 : Migration
    {
        
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Utilisateurs_Entreprises_EntrepriseId",
                table: "Utilisateurs");

            migrationBuilder.CreateTable(
                name: "CompteurFactures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Annee = table.Column<int>(type: "integer", nullable: false),
                    Mois = table.Column<int>(type: "integer", nullable: false),
                    DernierNumero = table.Column<int>(type: "integer", nullable: false),
                    Prefixe = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompteurFactures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreePar = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Statut = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TypeFacture = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ModePaiement = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Devise = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DateEmission = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateEcheance = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DatePaiement = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalHt = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    TotalTva = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    TotalTtc = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    MontantPaye = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConditionsPaiement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    XmlTeif = table.Column<string>(type: "text", nullable: true),
                    VersionTeif = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    HashIntegrite = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FactureOrigineId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoriqueFactures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactureId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectuePar = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AncienneValeur = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NouvelleValeur = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoriqueFactures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoriqueFactures_Factures_FactureId",
                        column: x => x.FactureId,
                        principalTable: "Factures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LignesFacture",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactureId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProduitId = table.Column<Guid>(type: "uuid", nullable: true),
                    Ordre = table.Column<int>(type: "integer", nullable: false),
                    Designation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Unite = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantite = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    PrixUnitaire = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    TauxRemise = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    TauxTva = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    MontantHt = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    MontantRemise = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    MontantTva = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    MontantTtc = table.Column<decimal>(type: "numeric(15,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LignesFacture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LignesFacture_Factures_FactureId",
                        column: x => x.FactureId,
                        principalTable: "Factures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompteurFactures_EntrepriseId_Annee_Mois",
                table: "CompteurFactures",
                columns: new[] { "EntrepriseId", "Annee", "Mois" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Factures_ClientId",
                table: "Factures",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Factures_EntrepriseId",
                table: "Factures",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_Factures_EntrepriseId_Numero",
                table: "Factures",
                columns: new[] { "EntrepriseId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Factures_Statut",
                table: "Factures",
                column: "Statut");

            migrationBuilder.CreateIndex(
                name: "IX_HistoriqueFactures_FactureId",
                table: "HistoriqueFactures",
                column: "FactureId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesFacture_FactureId",
                table: "LignesFacture",
                column: "FactureId");

            migrationBuilder.AddForeignKey(
                name: "FK_Utilisateurs_Entreprises_EntrepriseId",
                table: "Utilisateurs",
                column: "EntrepriseId",
                principalTable: "Entreprises",
                principalColumn: "Id");
        }

        
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Utilisateurs_Entreprises_EntrepriseId",
                table: "Utilisateurs");

            migrationBuilder.DropTable(
                name: "CompteurFactures");

            migrationBuilder.DropTable(
                name: "HistoriqueFactures");

            migrationBuilder.DropTable(
                name: "LignesFacture");

            migrationBuilder.DropTable(
                name: "Factures");

            migrationBuilder.AddForeignKey(
                name: "FK_Utilisateurs_Entreprises_EntrepriseId",
                table: "Utilisateurs",
                column: "EntrepriseId",
                principalTable: "Entreprises",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
