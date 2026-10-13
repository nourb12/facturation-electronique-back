using System;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Einvoicing.Infrastructure.Migrations;

[DbContext(typeof(ContextBaseDeDonnees))]
[Migration("20261006110000_AddOtpDemandesAcces")]
public partial class AddOtpDemandesAcces : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OtpDemandesAcces",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                Telephone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Canal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                EmailCodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                SmsCodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                VerificationTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                EmailVerifie = table.Column<bool>(type: "boolean", nullable: false),
                SmsVerifie = table.Column<bool>(type: "boolean", nullable: false),
                EstUtilise = table.Column<bool>(type: "boolean", nullable: false),
                NbEchecs = table.Column<int>(type: "integer", nullable: false),
                CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpireLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_OtpDemandesAcces", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_OtpDemandesAcces_Email_Telephone_EstUtilise_ExpireLe",
            table: "OtpDemandesAcces",
            columns: new[] { "Email", "Telephone", "EstUtilise", "ExpireLe" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "OtpDemandesAcces");
}
