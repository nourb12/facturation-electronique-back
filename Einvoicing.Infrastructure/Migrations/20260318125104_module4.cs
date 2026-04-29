using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    
    public partial class module4 : Migration
    {
        
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Echanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactureId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvoyePar = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Statut = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReponseCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReponseMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MotifRejet = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    DerniereAttempteLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AccepteeA = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejeteeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Echanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Paiements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactureId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnregistrePar = table.Column<Guid>(type: "uuid", nullable: false),
                    Montant = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    Devise = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Banque = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DatePaiement = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paiements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Signatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactureId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DemandeePar = table.Column<Guid>(type: "uuid", nullable: false),
                    Statut = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SignatureValue = table.Column<string>(type: "text", nullable: true),
                    CertificatId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MessageErreur = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NbTentatives = table.Column<int>(type: "integer", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SigneeA = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EchoueeA = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Signatures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Echanges_CorrelationId",
                table: "Echanges",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Echanges_EntrepriseId",
                table: "Echanges",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_Echanges_FactureId",
                table: "Echanges",
                column: "FactureId");

            migrationBuilder.CreateIndex(
                name: "IX_Paiements_EntrepriseId",
                table: "Paiements",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_Paiements_FactureId",
                table: "Paiements",
                column: "FactureId");

            migrationBuilder.CreateIndex(
                name: "IX_Signatures_EntrepriseId",
                table: "Signatures",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_Signatures_FactureId",
                table: "Signatures",
                column: "FactureId");
        }

        
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Echanges");

            migrationBuilder.DropTable(
                name: "Paiements");

            migrationBuilder.DropTable(
                name: "Signatures");
        }
    }
}
