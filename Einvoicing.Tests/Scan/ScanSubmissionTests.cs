using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Einvoicing.Tests.Scan;

public class ScanSubmissionTests
{
    private readonly Mock<IScannedDocumentRepository> _documents = new();
    private readonly Mock<ITransactionRepository> _transactions = new();

    private ScanService Service() => new(Mock.Of<IOcrClient>(), Mock.Of<IFileStorageService>(),
        _documents.Object, _transactions.Object, Mock.Of<IFournisseurRepository>(), Mock.Of<IOcrErrorDetectionService>());

    [Fact]
    public async Task AlreadyLinkedScan_ReturnsSameTransactionWithoutCreatingAnother()
    {
        var enterprise = Guid.NewGuid();
        var user = Guid.NewGuid();
        var document = NewDocument(enterprise, user);
        var transaction = Guid.NewGuid();
        document.AssocierTransaction(user, transaction);
        _documents.Setup(r => r.ObtenirParIdAsync(document.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);

        var result = await Service().ValidateAsync(enterprise, user, new ValidateScanRequest(document.Id, []));

        Assert.Equal(transaction, result.TransactionId);
        Assert.Equal("LinkedToTransaction", result.ReviewStatus);
        _transactions.Verify(r => r.AjouterAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _documents.Verify(r => r.SauvegarderAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AlreadyReviewedNonFinancialDocument_RemainsIdempotent()
    {
        var enterprise = Guid.NewGuid();
        var user = Guid.NewGuid();
        var document = NewDocument(enterprise, user);
        document.MarquerCommeReluMobile(user, "[]", "[]");
        _documents.Setup(r => r.ObtenirParIdAsync(document.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);

        var result = await Service().ValidateAsync(enterprise, user, new ValidateScanRequest(document.Id, []));

        Assert.Null(result.TransactionId);
        Assert.Equal("MobileReviewed", result.ReviewStatus);
        _documents.Verify(r => r.SauvegarderAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AlreadyLinkedScan_StillRejectsAnotherEnterprise()
    {
        var document = NewDocument(Guid.NewGuid(), Guid.NewGuid());
        document.AssocierTransaction(Guid.NewGuid(), Guid.NewGuid());
        _documents.Setup(r => r.ObtenirParIdAsync(document.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);
        await Assert.ThrowsAsync<AccesRefuseException>(() => Service().ValidateAsync(Guid.NewGuid(), Guid.NewGuid(),
            new ValidateScanRequest(document.Id, [])));
    }

    [Fact]
    public async Task MissingRequiredFields_AreRejectedBeforePersistence()
    {
        var enterprise = Guid.NewGuid();
        var user = Guid.NewGuid();
        var document = NewDocument(enterprise, user);
        _documents.Setup(r => r.ObtenirParIdAsync(document.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);
        await Assert.ThrowsAsync<ValidationMetierException>(() => Service().ValidateAsync(enterprise, user,
            new ValidateScanRequest(document.Id, [])));
        _documents.Verify(r => r.SauvegarderAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SimultaneousSubmission_RejectsStaleWriter()
    {
        var options = new DbContextOptionsBuilder<ContextBaseDeDonnees>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var first = new ContextBaseDeDonnees(options);
        var document = NewDocument(Guid.NewGuid(), Guid.NewGuid());
        first.ScannedDocuments.Add(document);
        await first.SaveChangesAsync();
        await using var second = new ContextBaseDeDonnees(options);
        var stale = await second.ScannedDocuments.SingleAsync();
        document.AssocierTransaction(Guid.NewGuid(), Guid.NewGuid());
        await first.SaveChangesAsync();
        stale.AssocierTransaction(Guid.NewGuid(), Guid.NewGuid());
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    private static ScannedDocument NewDocument(Guid enterprise, Guid user) =>
        ScannedDocument.Creer(enterprise, user, "facture", DocumentSource.MobileApp, "scan/a.jpg", "a.jpg", "image/jpeg", 20);
}
