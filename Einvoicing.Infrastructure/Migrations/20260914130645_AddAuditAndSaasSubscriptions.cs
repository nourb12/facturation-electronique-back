using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditAndSaasSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AbonnementsSaas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Plan = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Statut = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PrixMensuel = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    LimiteUtilisateurs = table.Column<int>(type: "integer", nullable: false),
                    LimiteFacturesMensuelles = table.Column<int>(type: "integer", nullable: false),
                    DateDebut = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProchainRenouvellement = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbonnementsSaas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvenementsAudit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: true),
                    UtilisateurId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Ressource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RessourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AdresseIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AgentUtilisateur = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CodeReponse = table.Column<int>(type: "integer", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvenementsAudit", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbonnementsSaas_EntrepriseId",
                table: "AbonnementsSaas",
                column: "EntrepriseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbonnementsSaas_Statut_ProchainRenouvellement",
                table: "AbonnementsSaas",
                columns: new[] { "Statut", "ProchainRenouvellement" });

            migrationBuilder.CreateIndex(
                name: "IX_EvenementsAudit_EntrepriseId_CreeLe",
                table: "EvenementsAudit",
                columns: new[] { "EntrepriseId", "CreeLe" });

            migrationBuilder.CreateIndex(
                name: "IX_EvenementsAudit_UtilisateurId",
                table: "EvenementsAudit",
                column: "UtilisateurId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbonnementsSaas");

            migrationBuilder.DropTable(
                name: "EvenementsAudit");
        }
    }
}
