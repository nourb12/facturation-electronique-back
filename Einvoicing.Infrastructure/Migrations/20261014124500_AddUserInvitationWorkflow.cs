using System;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations;

// Sans ces attributs (pas de fichier Designer), EF Core ignorait cette migration.
[DbContext(typeof(ContextBaseDeDonnees))]
[Migration("20261014124500_AddUserInvitationWorkflow")]
public partial class AddUserInvitationWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InvitationsUtilisateurs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UtilisateurId = table.Column<Guid>(type: "uuid", nullable: false),
                InvitePar = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpireLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                AccepteeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                EmailEnvoyeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InvitationsUtilisateurs", x => x.Id);
                table.ForeignKey(
                    name: "FK_InvitationsUtilisateurs_Utilisateurs_UtilisateurId",
                    column: x => x.UtilisateurId,
                    principalTable: "Utilisateurs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_InvitationsUtilisateurs_UtilisateurId",
            table: "InvitationsUtilisateurs",
            column: "UtilisateurId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "InvitationsUtilisateurs");
    }
}

