using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEntrepriseKycFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentsUploades",
                table: "Entreprises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DonneesInscription",
                table: "Entreprises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DonneesScoring",
                table: "Entreprises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreKyc",
                table: "Entreprises",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentsUploades",
                table: "Entreprises");

            migrationBuilder.DropColumn(
                name: "DonneesInscription",
                table: "Entreprises");

            migrationBuilder.DropColumn(
                name: "DonneesScoring",
                table: "Entreprises");

            migrationBuilder.DropColumn(
                name: "ScoreKyc",
                table: "Entreprises");
        }
    }
}
