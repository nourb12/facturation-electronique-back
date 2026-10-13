using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    public partial class AddKnowledgeBase : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Source = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    ExtractionError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_KnowledgeDocuments", x => x.Id));

            migrationBuilder.CreateTable(
                name: "KnowledgeChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Heading = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeChunks_KnowledgeDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "KnowledgeDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeChunks_DocumentId_Position",
                table: "KnowledgeChunks",
                columns: new[] { "DocumentId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeDocuments_EntrepriseId_Status",
                table: "KnowledgeDocuments",
                columns: new[] { "EntrepriseId", "Status" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "KnowledgeChunks");
            migrationBuilder.DropTable(name: "KnowledgeDocuments");
        }
    }
}
