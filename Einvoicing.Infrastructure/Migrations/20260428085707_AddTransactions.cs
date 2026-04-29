using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreePar = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiePar = table.Column<Guid>(type: "uuid", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Libelle = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    TiersNom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CategorieNom = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Statut = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatutJustificatif = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Montant = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    Devise = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Compte = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FactureId = table.Column<Guid>(type: "uuid", nullable: true),
                    JustificatifChemin = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    JustificatifNomFichier = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    JustificatifContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    JustificatifTailleOctets = table.Column<long>(type: "bigint", nullable: true),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_EntrepriseId",
                table: "Transactions",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_EntrepriseId_Date",
                table: "Transactions",
                columns: new[] { "EntrepriseId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_EntrepriseId_Statut",
                table: "Transactions",
                columns: new[] { "EntrepriseId", "Statut" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_FactureId",
                table: "Transactions",
                column: "FactureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transactions");
        }
    }
}
