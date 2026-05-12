using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseReviewAndSuppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountingPeriodLabel",
                table: "Transactions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivitiesJson",
                table: "Transactions",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "AllocationsJson",
                table: "Transactions",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "BankMatchJson",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommentsJson",
                table: "Transactions",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "Transactions",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FournisseurId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FournisseurMatriculeFiscal",
                table: "Transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MissingFieldsJson",
                table: "Transactions",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "OcrOverallConfidence",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RecoverableVatAmount",
                table: "Transactions",
                type: "numeric(15,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RecoverableVatRate",
                table: "Transactions",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewFieldsJson",
                table: "Transactions",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "Fournisseurs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MatriculeFiscal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Adresse = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Iban = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Telephone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fournisseurs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_EntrepriseId_Source",
                table: "Transactions",
                columns: new[] { "EntrepriseId", "Source" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_FournisseurId",
                table: "Transactions",
                column: "FournisseurId");

            migrationBuilder.CreateIndex(
                name: "IX_Fournisseurs_EntrepriseId",
                table: "Fournisseurs",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_Fournisseurs_EntrepriseId_MatriculeFiscal",
                table: "Fournisseurs",
                columns: new[] { "EntrepriseId", "MatriculeFiscal" });

            migrationBuilder.CreateIndex(
                name: "IX_Fournisseurs_EntrepriseId_Nom",
                table: "Fournisseurs",
                columns: new[] { "EntrepriseId", "Nom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Fournisseurs");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_EntrepriseId_Source",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_FournisseurId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AccountingPeriodLabel",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ActivitiesJson",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AllocationsJson",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BankMatchJson",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CommentsJson",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FournisseurId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FournisseurMatriculeFiscal",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "MissingFieldsJson",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "OcrOverallConfidence",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RecoverableVatAmount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RecoverableVatRate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ReviewFieldsJson",
                table: "Transactions");
        }
    }
}
