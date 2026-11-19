using Einvoicing.Api.Controllers;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Einvoicing.Tests.Scan;

public class MobileDocumentUpdatesTests
{
    [Theory]
    [InlineData(StatutTransaction.Justifiee, "reviewed")]
    [InlineData(StatutTransaction.EnAttente, "pending")]
    [InlineData(StatutTransaction.NonJustifiee, "correction")]
    public async Task Feed_IsRestrictedToOwnerAndEnterprise_AndUsesActualState(StatutTransaction status, string kind)
    {
        var options = new DbContextOptionsBuilder<ContextBaseDeDonnees>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ContextBaseDeDonnees(options);
        var enterprise = Guid.NewGuid();
        var actor = Guid.NewGuid();
        Add(db, enterprise, actor, status);
        Add(db, enterprise, Guid.NewGuid(), StatutTransaction.NonJustifiee);
        Add(db, Guid.NewGuid(), actor, StatutTransaction.NonJustifiee);
        await db.SaveChangesAsync();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.EntrepriseId).Returns(enterprise);
        user.SetupGet(x => x.UtilisateurId).Returns(actor);
        var result = Assert.IsType<OkObjectResult>(await new MobileDocumentUpdatesController(db, user.Object).List(default));
        var rows = Assert.IsAssignableFrom<IEnumerable<MobileDocumentUpdate>>(result.Value).ToList();
        Assert.Single(rows);
        Assert.Equal(kind, rows[0].Kind);
    }

    [Fact]
    public async Task Feed_RejectsMissingIdentity()
    {
        await using var db = new ContextBaseDeDonnees(new DbContextOptionsBuilder<ContextBaseDeDonnees>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Assert.IsType<UnauthorizedResult>(await new MobileDocumentUpdatesController(db, Mock.Of<ICurrentUserService>()).List(default));
    }

    private static void Add(ContextBaseDeDonnees db, Guid enterprise, Guid actor, StatutTransaction status)
    {
        var transaction = Transaction.Creer(enterprise, actor, DateTime.UtcNow, "Document", 12m, TypeTransaction.Sortie, statut: status);
        var document = ScannedDocument.Creer(enterprise, actor, "facture", DocumentSource.MobileApp, "scan/a.jpg", "a.jpg", "image/jpeg", 10);
        document.AssocierTransaction(actor, transaction.Id);
        db.Transactions.Add(transaction);
        db.ScannedDocuments.Add(document);
    }
}
