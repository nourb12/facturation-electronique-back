using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations
{
    
    public partial class InitialCreate : Migration
    {
        
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EstActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MatriculeFiscal = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    Adresse = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Ville = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CodePostal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Pays = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Telephone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TypeClient = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Entreprises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MatriculeFiscal = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    Adresse = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Ville = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CodePostal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Pays = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Telephone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SiteWeb = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegimeFiscal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CodeTva = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ParametresTeif = table.Column<string>(type: "text", nullable: true),
                    VersionTeif = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EstActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entreprises", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Produits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategorieId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PrixUnitaire = table.Column<decimal>(type: "numeric(15,3)", nullable: false),
                    TauxTva = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    Unite = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Produits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Produits_Categories_CategorieId",
                        column: x => x.CategorieId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Utilisateurs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Prenom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    MotDePasseHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Telephone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Poste = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Departement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Statut = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EntrepriseId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeuxFAActif = table.Column<bool>(type: "boolean", nullable: false),
                    DeuxFASecret = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AlerteConnexion = table.Column<bool>(type: "boolean", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DerniereConnexion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstSupprime = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utilisateurs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Utilisateurs_Entreprises_EntrepriseId",
                        column: x => x.EntrepriseId,
                        principalTable: "Entreprises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OtpCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UtilisateurId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EstUtilise = table.Column<bool>(type: "boolean", nullable: false),
                    NbEchecs = table.Column<int>(type: "integer", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpireLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtpCodes_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UtilisateurId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    JwtId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EstUtilise = table.Column<bool>(type: "boolean", nullable: false),
                    EstRevoque = table.Column<bool>(type: "boolean", nullable: false),
                    AdresseIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpireLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UtilisateurId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefreshTokenRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Appareil = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TypeAppareil = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Localisation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AdresseIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DerniereActivite = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_EntrepriseId",
                table: "Categories",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_EntrepriseId",
                table: "Clients",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_EntrepriseId_Email",
                table: "Clients",
                columns: new[] { "EntrepriseId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entreprises_MatriculeFiscal",
                table: "Entreprises",
                column: "MatriculeFiscal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_UtilisateurId_Type_EstUtilise_ExpireLe",
                table: "OtpCodes",
                columns: new[] { "UtilisateurId", "Type", "EstUtilise", "ExpireLe" });

            migrationBuilder.CreateIndex(
                name: "IX_Produits_CategorieId",
                table: "Produits",
                column: "CategorieId");

            migrationBuilder.CreateIndex(
                name: "IX_Produits_EntrepriseId",
                table: "Produits",
                column: "EntrepriseId");

            migrationBuilder.CreateIndex(
                name: "IX_Produits_EntrepriseId_Code",
                table: "Produits",
                columns: new[] { "EntrepriseId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UtilisateurId",
                table: "RefreshTokens",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UtilisateurId",
                table: "Sessions",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_Utilisateurs_Email",
                table: "Utilisateurs",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Utilisateurs_EntrepriseId",
                table: "Utilisateurs",
                column: "EntrepriseId");
        }

        
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "OtpCodes");

            migrationBuilder.DropTable(
                name: "Produits");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Utilisateurs");

            migrationBuilder.DropTable(
                name: "Entreprises");
        }
    }
}
