using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierInvoicesAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentsCommerciaux",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreePar = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Numero = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Statut = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DateDocument = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateEcheance = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalHt = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    TotalTva = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    TotalTtc = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    LignesJson = table.Column<string>(type: "text", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DocumentOrigineId = table.Column<Guid>(type: "uuid", nullable: true),
                    FactureCreeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentsCommerciaux", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentsCommerciaux_ClientId",
                table: "DocumentsCommerciaux",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentsCommerciaux_EntrepriseId_Numero",
                table: "DocumentsCommerciaux",
                columns: new[] { "EntrepriseId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentsCommerciaux");
        }
    }
}
