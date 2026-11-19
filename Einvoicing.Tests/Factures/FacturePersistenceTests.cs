using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Einvoicing.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Einvoicing.Tests.Factures;

/// <summary>
/// Cycle de vie d'une facture enregistrée, chaque étape dans un nouveau contexte (= une requête HTTP).
/// </summary>
public sealed class FacturePersistenceTests
{
    private readonly DbContextOptions<ContextBaseDeDonnees> _options = new DbContextOptionsBuilder<ContextBaseDeDonnees>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    private async Task<(Guid factureId, Guid entrepriseId)> CreerFactureEnregistreeAsync()
    {
        await using var db = new ContextBaseDeDonnees(_options);
        var entrepriseId = Guid.NewGuid();
        var client = Client.Creer(entrepriseId, "Client", "client@test.tn", TypeClient.B2B, "1234567ABM000");
        db.Clients.Add(client);
        var facture = Facture.Creer(entrepriseId, client.Id, Guid.NewGuid(), "FAC-TEST-0001", TypeFacture.Facture,
            ModePaiement.Virement, DateTime.UtcNow.AddDays(30), remiseGlobale: 10m);
        facture.AjouterLigne(LigneFacture.Creer(facture.Id, 1, "Service", 1, 100m, 19m));
        db.Factures.Add(facture);
        await db.SaveChangesAsync();
        return (facture.Id, entrepriseId);
    }

    private static FactureService Service(ContextBaseDeDonnees db)
        => new(new FactureRepository(db), new ClientRepository(db),
            new NumeroFactureService(new CompteurFactureRepository(db)),
            new EntrepriseRepository(db), new PersonnalisationRepository(db));

    [Fact]
    public async Task Validation_DUneFactureEnregistree_EstPersistee()
    {
        var (factureId, entrepriseId) = await CreerFactureEnregistreeAsync();

        await using (var db = new ContextBaseDeDonnees(_options))
        {
            var dto = await Service(db).ValiderAsync(factureId, entrepriseId, Guid.NewGuid());
            dto.Statut.Should().Be("Validee");
        }

        await using (var db = new ContextBaseDeDonnees(_options))
        {
            var facture = await new FactureRepository(db).ObtenirAvecDetailsAsync(factureId);
            facture!.Statut.Should().Be(StatutFacture.Validee);
            facture.Historique.Should().HaveCount(2);
            facture.TotalTtc.Should().Be(107.1m);
            facture.NetAPayer.Should().Be(108.1m);
        }
    }

    private async Task EtapeAsync(Guid factureId, Action<Facture> action)
    {
        await using var db = new ContextBaseDeDonnees(_options);
        var repo = new FactureRepository(db);
        var facture = await repo.ObtenirAvecDetailsAsync(factureId);
        action(facture!);
        repo.MettreAJour(facture!);
        await repo.SauvegarderAsync();
    }

    [Fact]
    public async Task CycleComplet_JusquAuPaiement_ChaqueEtapeDansUneNouvelleRequete()
    {
        var (factureId, entrepriseId) = await CreerFactureEnregistreeAsync();
        var user = Guid.NewGuid();

        await EtapeAsync(factureId, f => f.ValiderMetier(user));
        await EtapeAsync(factureId, f => f.MarquerConforme(user, "1.8.8"));
        await EtapeAsync(factureId, f => f.MarquerTransmise(user));
        await EtapeAsync(factureId, f => f.MarquerAcceptee(user));

        async Task<Einvoicing.Application.DTOs.PaiementDto> Payer(decimal montant)
        {
            await using var db = new ContextBaseDeDonnees(_options);
            var service = new PaiementService(new PaiementRepository(db), new FactureRepository(db));
            return await service.EnregistrerAsync(entrepriseId, user,
                new Einvoicing.Application.DTOs.EnregistrerPaiementRequest(factureId, montant, ModePaiement.Virement, DateTime.UtcNow, "REF", null, null));
        }

        var surpaiement = () => Payer(1000m);
        await surpaiement.Should().ThrowAsync<Einvoicing.Domain.Exceptions.ValidationMetierException>();
        await Payer(50m);
        await Payer(58.1m);

        await using var verif = new ContextBaseDeDonnees(_options);
        var finale = await new FactureRepository(verif).ObtenirAvecDetailsAsync(factureId);
        finale!.Statut.Should().Be(StatutFacture.Payee);
        finale.MontantRestant.Should().Be(0);
        finale.Historique.Should().HaveCount(7); // creation, validation, conformite, transmission, acceptation, 2 paiements
    }

    [Fact]
    public async Task MettreAJour_DUneEntiteJusteAjoutee_LInsereSansErreurDeConcurrence()
    {
        await using var db = new ContextBaseDeDonnees(_options);
        var repo = new EchangeRepository(db);
        var echange = ExternalExchange.Creer(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await repo.AjouterAsync(echange);
        echange.MarquerEnvoye();
        repo.MettreAJour(echange);   // même schéma que l'envoi TTN
        await repo.SauvegarderAsync();

        await using var verif = new ContextBaseDeDonnees(_options);
        (await verif.Echanges.FindAsync(echange.Id)).Should().NotBeNull();
    }
}
