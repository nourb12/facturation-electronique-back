using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScannedDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Web");

            migrationBuilder.CreateTable(
                name: "ScannedDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreePar = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiePar = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    OverallConfidence = table.Column<int>(type: "integer", nullable: false),
                    RawText = table.Column<string>(type: "text", nullable: false),
                    FieldsJson = table.Column<string>(type: "text", nullable: false),
                    MissingFieldsJson = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValideLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScannedDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScannedDocuments_EntrepriseId",
                table: "ScannedDocuments",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_ScannedDocuments_Status",
                table: "ScannedDocuments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ScannedDocuments_TransactionId",
                table: "ScannedDocuments",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScannedDocuments");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Transactions");
        }
    }
}
