




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Exceptions;
using System.Text;

namespace Einvoicing.Application.Services;

public sealed class FactureService(
    IFactureRepository factureRepo,
    IClientRepository clientRepo,
    INumeroFactureService numeroService,
    IEntrepriseRepository entrepriseRepo,
    IPersonnalisationRepository personnalisationRepo
) : IFactureService
{
    
    
    

    public async Task<FactureDto> CreerAsync(
        Guid entrepriseId, Guid creePar,
        CreerFactureRequest req, CancellationToken ct = default)
    {
        var client = await clientRepo.ObtenirParIdAsync(req.ClientId, ct)
            ?? throw new NotFoundException("Client introuvable.");
        if (client.EntrepriseId != entrepriseId)
            throw new AccesRefuseException("Client n'appartient pas à cette entreprise.");
        if (!client.EstActif)
            throw new ValidationMetierException("Le client est inactif.");

        if (req.TypeFacture == Domain.Enums.TypeFacture.Avoir)
        {
            if (req.FactureOrigineId is null)
                throw new ValidationMetierException("Un avoir doit etre lie a une facture d'origine.");

            var origine = await factureRepo.ObtenirParIdAsync(req.FactureOrigineId.Value, ct)
                ?? throw new NotFoundException("Facture d'origine introuvable.");
            if (origine.EntrepriseId != entrepriseId || origine.ClientId != req.ClientId)
                throw new ValidationMetierException("L'avoir doit viser une facture du meme client et de la meme entreprise.");
            if (origine.TypeFacture == Domain.Enums.TypeFacture.Avoir)
                throw new ValidationMetierException("Un avoir ne peut pas avoir un autre avoir comme origine.");
        }

        var numero = await numeroService.GenererNumeroAsync(entrepriseId, ct);

        var facture = Facture.Creer(
            entrepriseId, req.ClientId, creePar,
            numero, req.TypeFacture, req.ModePaiement,
            req.DateEcheance, req.Reference, req.Notes,
            req.ConditionsPaiement, req.Devise, req.AppliquerRS,
            req.CodeRS, req.TauxRS, req.FactureOrigineId);

        int ordre = 1;
        foreach (var ligneReq in req.Lignes)
        {
            var ligne = LigneFacture.Creer(
                facture.Id, ordre++,
                ligneReq.Designation, ligneReq.Quantite,
                ligneReq.PrixUnitaire, ligneReq.TauxTva,
                ligneReq.ProduitId, ligneReq.Description,
                ligneReq.Unite, ligneReq.TauxRemise);
            facture.AjouterLigne(ligne);
        }

        await factureRepo.AjouterAsync(facture, ct);
        await factureRepo.SauvegarderAsync(ct);

        return await MapToDtoAsync(facture, client, ct);
    }

    
    
    

    public async Task<FactureDto> ObtenirParIdAsync(
        Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(id, ct)
            ?? throw new NotFoundException("Facture introuvable.");
        if (facture.EntrepriseId != entrepriseId)
            throw new AccesRefuseException();

        var client = await clientRepo.ObtenirParIdAsync(facture.ClientId, ct)
            ?? throw new NotFoundException("Client introuvable.");

        return await MapToDtoAsync(facture, client, ct);
    }

    
    
    

    public async Task<ListeFacturesDto> ListerAsync(
        Guid entrepriseId, FiltreFacturesRequest filtre, CancellationToken ct = default)
    {
        var (items, total) = await factureRepo.ListerAsync(entrepriseId, filtre, ct);

        var dtos = new List<FactureListeDto>();
        foreach (var f in items)
        {
            var client = await clientRepo.ObtenirParIdAsync(f.ClientId, ct);
            dtos.Add(new FactureListeDto(
                f.Id, f.Numero, client?.Nom ?? "-",
                f.Statut.ToString(), f.TypeFacture.ToString(),
                f.DateEmission, f.DateEcheance,
                f.TotalTtc, f.AppliquerRS, f.MontantRS, f.NetAPayer,
                f.MontantPaye, f.EstEnRetard, f.Devise));
        }

        return new ListeFacturesDto(dtos, total, filtre.Page, filtre.ParPage);
    }

    
    
    

    public async Task<FactureDto> MettreAJourAsync(
        Guid id, Guid entrepriseId, Guid modifiePar,
        MettreAJourFactureRequest req, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(id, ct)
            ?? throw new NotFoundException("Facture introuvable.");
        if (facture.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        facture.MettreAJourInfos(
            req.ModePaiement, req.DateEcheance,
            req.Reference, req.Notes, req.ConditionsPaiement,
            req.AppliquerRS, req.CodeRS, req.TauxRS);

        foreach (var ligneExistante in facture.Lignes.ToList())
            facture.SupprimerLigne(ligneExistante.Id);

        int ordre = 1;
        foreach (var ligneReq in req.Lignes)
        {
            var ligne = LigneFacture.Creer(
                facture.Id, ordre++,
                ligneReq.Designation, ligneReq.Quantite,
                ligneReq.PrixUnitaire, ligneReq.TauxTva,
                ligneReq.ProduitId, ligneReq.Description,
                ligneReq.Unite, ligneReq.TauxRemise);
            facture.AjouterLigne(ligne);
        }

        factureRepo.MettreAJour(facture);
        await factureRepo.SauvegarderAsync(ct);

        var client = await clientRepo.ObtenirParIdAsync(facture.ClientId, ct);
        return await MapToDtoAsync(facture, client!, ct);
    }

    
    
    

    public async Task<FactureDto> ValiderAsync(
        Guid id, Guid entrepriseId, Guid validePar, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(id, ct)
            ?? throw new NotFoundException("Facture introuvable.");
        if (facture.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        facture.ValiderMetier(validePar);
        factureRepo.MettreAJour(facture);
        await factureRepo.SauvegarderAsync(ct);

        var client = await clientRepo.ObtenirParIdAsync(facture.ClientId, ct);
        return await MapToDtoAsync(facture, client!, ct);
    }

    public async Task<FactureDto> RejeterAsync(
        Guid id, Guid entrepriseId, Guid rejetePar,
        RejeterFactureRequest req, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(id, ct)
            ?? throw new NotFoundException("Facture introuvable.");
        if (facture.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        facture.MarquerRejetee(rejetePar, req.Motif);
        factureRepo.MettreAJour(facture);
        await factureRepo.SauvegarderAsync(ct);

        var client = await clientRepo.ObtenirParIdAsync(facture.ClientId, ct);
        return await MapToDtoAsync(facture, client!, ct);
    }

    public async Task<FactureDto> AnnulerAsync(
        Guid id, Guid entrepriseId, Guid annulerPar,
        AnnulerFactureRequest req, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(id, ct)
            ?? throw new NotFoundException("Facture introuvable.");
        if (facture.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        facture.Annuler(annulerPar, req.Motif);
        factureRepo.MettreAJour(facture);
        await factureRepo.SauvegarderAsync(ct);

        var client = await clientRepo.ObtenirParIdAsync(facture.ClientId, ct);
        return await MapToDtoAsync(facture, client!, ct);
    }

    public async Task<FactureDto> RemettreBrouillonAsync(
        Guid id, Guid entrepriseId, Guid remisePar, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(id, ct)
            ?? throw new NotFoundException("Facture introuvable.");
        if (facture.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        facture.RemettreBrouillon(remisePar);
        factureRepo.MettreAJour(facture);
        await factureRepo.SauvegarderAsync(ct);

        var client = await clientRepo.ObtenirParIdAsync(facture.ClientId, ct);
        return await MapToDtoAsync(facture, client!, ct);
    }

    public async Task<FactureDto> ConvertirEnFactureAsync(
        Guid id, Guid entrepriseId, Guid creePar, ConvertirFactureRequest req, CancellationToken ct = default)
    {
        var source = await factureRepo.ObtenirAvecDetailsAsync(id, ct)
            ?? throw new NotFoundException("Document source introuvable.");
        if (source.EntrepriseId != entrepriseId) throw new AccesRefuseException();
        if (source.TypeFacture == Domain.Enums.TypeFacture.Facture)
            throw new ValidationMetierException("Ce document est deja une facture.");

        var request = new CreerFactureRequest(
            source.ClientId,
            Domain.Enums.TypeFacture.Facture,
            source.ModePaiement,
            req.DateEcheance ?? source.DateEcheance,
            source.Lignes.OrderBy(l => l.Ordre).Select(l => new CreerLigneFactureRequest(
                l.Designation,
                l.Quantite,
                l.PrixUnitaire,
                l.TauxTva,
                l.ProduitId,
                l.Description,
                l.Unite,
                l.TauxRemise)).ToList(),
            req.Reference ?? $"Convertie depuis {source.Numero}",
            source.Notes,
            source.ConditionsPaiement,
            source.Devise,
            source.AppliquerRS,
            source.CodeRS,
            source.TauxRS,
            source.Id);

        return await CreerAsync(entrepriseId, creePar, request, ct);
    }

    public async Task<StatistiquesFacturesDto> ObtenirStatistiquesAsync(
        Guid entrepriseId, CancellationToken ct = default)
        => await factureRepo.ObtenirStatistiquesAsync(entrepriseId, ct);

    
    
    

    public async Task<byte[]> GenererPdfAsync(
        Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(id, ct)
            ?? throw new NotFoundException("Facture introuvable.");
        if (facture.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        var client = await clientRepo.ObtenirParIdAsync(facture.ClientId, ct)
            ?? throw new NotFoundException("Client introuvable.");
        var entreprise = await entrepriseRepo.ObtenirParIdAsync(entrepriseId, ct)
            ?? throw new NotFoundException("Entreprise introuvable.");
        var personnalisation = await personnalisationRepo.ObtenirAsync(entrepriseId, ct);

        return FacturePdfBuilder.Generate(facture, client, entreprise, personnalisation?.DonneesJson);
    }
    private static string BuildFactureHtml(
        Facture f, Client client, Einvoicing.Domain.Entities.Entreprise ent)
    {
        var lignesHtml = new StringBuilder();
        foreach (var l in f.Lignes.OrderBy(x => x.Ordre))
        {
            lignesHtml.Append($@"
            <tr>
                <td>{System.Net.WebUtility.HtmlEncode(l.Designation)}</td>
                <td class='center'>{l.Quantite:N3}</td>
                <td class='right'>{l.PrixUnitaire:N3}</td>
                <td class='center'>{l.TauxRemise:N1}%</td>
                <td class='center'>{l.TauxTva:N0}%</td>
                <td class='right'>{l.MontantHt:N3}</td>
                <td class='right'>{l.MontantTtc:N3}</td>
            </tr>");
        }

        var statutColor = f.Statut.ToString() switch
        {
            "Brouillon" => "#64748B",
            "Validee" => "#3B82F6",
            "Conforme" => "#8B5CF6",
            "Transmise" => "#F59E0B",
            "Acceptee" => "#22C55E",
            "Rejetee" => "#EF4444",
            "Payee" => "#22C55E",
            "Annulee" => "#94A3B8",
            _ => "#64748B"
        };

        var now = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        return $@"<!DOCTYPE html>
<html lang=""fr"">
<head>
<meta charset=""UTF-8""/>
<title>Facture {System.Net.WebUtility.HtmlEncode(f.Numero)}</title>
<style>
  @import url('https://fonts.googleapis.com/css2?family=DM+Sans:wght@300;400;500;600;700&display=swap');
  *{{ box-sizing:border-box; margin:0; padding:0; }}
  body{{ font-family:'DM Sans',sans-serif; background:#f8f8f6; color:#1a1a1a; font-size:13px; line-height:1.5; }}
  .page{{ max-width:860px; margin:0 auto; background:#fff; padding:48px 52px; min-height:100vh; }}
  .header{{ display:flex; justify-content:space-between; align-items:flex-start; margin-bottom:48px; }}
  .brand-mark{{ width:40px; height:40px; background:#1a1a1a; border-radius:10px; display:flex; align-items:center; justify-content:center; font-weight:900; font-size:13px; color:#fff; }}
  .invoice-num{{ font-size:22px; font-weight:700; color:#1a1a1a; }}
  .statut-badge{{ display:inline-block; margin-top:6px; padding:3px 12px; border-radius:99px; font-size:10px; font-weight:700; background:{statutColor}22; color:{statutColor}; border:1px solid {statutColor}44; }}
  .divider{{ height:1px; background:#e8e8e8; margin:28px 0; }}
  .parties{{ display:grid; grid-template-columns:1fr 1fr; gap:32px; margin-bottom:36px; }}
  .party-box{{ padding:20px; border-radius:10px; background:#fafafa; border:1px solid #efefef; }}
  .party-label{{ font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:0.1em; color:#aaa; margin-bottom:10px; }}
  .party-name{{ font-size:14px; font-weight:700; color:#1a1a1a; margin-bottom:4px; }}
  .party-detail{{ font-size:11px; color:#666; line-height:1.7; }}
  .dates-row{{ display:grid; grid-template-columns:repeat(3,1fr); gap:16px; margin-bottom:32px; }}
  .date-card{{ padding:14px 16px; border-radius:8px; background:#fafafa; border:1px solid #efefef; }}
  .date-label{{ font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:0.1em; color:#aaa; margin-bottom:4px; }}
  .date-val{{ font-size:13px; font-weight:600; color:#1a1a1a; }}
  table{{ width:100%; border-collapse:collapse; margin-bottom:24px; }}
  thead tr{{ background:#1a1a1a; }}
  thead th{{ padding:10px 12px; text-align:left; font-size:10px; font-weight:600; text-transform:uppercase; letter-spacing:0.06em; color:#fff; }}
  thead th.right{{ text-align:right; }} thead th.center{{ text-align:center; }}
  tbody tr{{ border-bottom:1px solid #f0f0f0; }}
  tbody td{{ padding:10px 12px; font-size:12px; color:#333; }}
  tbody td.right{{ text-align:right; font-family:monospace; font-size:11px; font-weight:600; }}
  tbody td.center{{ text-align:center; }}
  .totals{{ display:flex; justify-content:flex-end; margin-bottom:36px; }}
  .totals-box{{ width:280px; }}
  .total-row{{ display:flex; justify-content:space-between; padding:7px 0; border-bottom:1px solid #f0f0f0; font-size:12px; }}
  .total-final{{ display:flex; justify-content:space-between; padding:12px 16px; background:#1a1a1a; border-radius:8px; margin-top:8px; }}
  .total-final span:first-child{{ font-size:12px; font-weight:600; color:#fff; }}
  .total-final span:last-child{{ font-size:16px; font-weight:700; color:#fff; font-family:monospace; }}
  .footer{{ border-top:1px solid #e8e8e8; padding-top:20px; display:flex; justify-content:space-between; }}
  .footer-brand{{ font-size:10px; color:#aaa; }}
  @media print{{ body{{ background:#fff; }} .page{{ padding:20mm; }} @page{{ margin:0; size:A4; }} }}
</style>
</head>
<body>
<div class=""page"">
  <div class=""header"">
    <div style=""display:flex;align-items:center;gap:12px"">
      <div class=""brand-mark"">TF</div>
      <div>
        <div style=""font-size:16px;font-weight:700"">{System.Net.WebUtility.HtmlEncode(ent.Nom)}</div>
        <div style=""font-size:10px;color:#888"">Portail de facturation electronique certifiee</div>
      </div>
    </div>
    <div style=""text-align:right"">
      <div class=""invoice-num"">FACTURE N° {System.Net.WebUtility.HtmlEncode(f.Numero)}</div>
      <div class=""statut-badge"">{f.Statut}</div>
    </div>
  </div>
  <div class=""divider""></div>
  <div class=""parties"">
    <div class=""party-box"">
      <div class=""party-label"">Émetteur</div>
      <div class=""party-name"">{System.Net.WebUtility.HtmlEncode(ent.Nom)}</div>
      <div class=""party-detail"">{System.Net.WebUtility.HtmlEncode(ent.Adresse)}<br/>{System.Net.WebUtility.HtmlEncode(ent.Email)}</div>
      <div style=""font-family:monospace;font-size:10px;margin-top:6px;color:#555"">MF : {System.Net.WebUtility.HtmlEncode(ent.MatriculeFiscal)}</div>
    </div>
    <div class=""party-box"">
      <div class=""party-label"">Client</div>
      <div class=""party-name"">{System.Net.WebUtility.HtmlEncode(client.Nom)}</div>
      <div class=""party-detail"">{System.Net.WebUtility.HtmlEncode(client.Email)}</div>
      {(client.MatriculeFiscal != null ? $"<div style='font-family:monospace;font-size:10px;margin-top:6px;color:#555'>MF : {System.Net.WebUtility.HtmlEncode(client.MatriculeFiscal)}</div>" : "")}
    </div>
  </div>
  <div class=""dates-row"">
    <div class=""date-card""><div class=""date-label"">Date d'émission</div><div class=""date-val"">{f.DateEmission:dd/MM/yyyy}</div></div>
    <div class=""date-card""><div class=""date-label"">Date d'échéance</div><div class=""date-val"">{f.DateEcheance:dd/MM/yyyy}</div></div>
    <div class=""date-card""><div class=""date-label"">Mode de paiement</div><div class=""date-val"">{f.ModePaiement}</div></div>
  </div>
  <table>
    <thead><tr>
      <th>Désignation</th><th class=""center"">Qté</th><th class=""right"">P.U. HT</th>
      <th class=""center"">Remise</th><th class=""center"">TVA</th>
      <th class=""right"">HT</th><th class=""right"">TTC</th>
    </tr></thead>
    <tbody>{lignesHtml}</tbody>
  </table>
  <div class=""totals"">
    <div class=""totals-box"">
      <div class=""total-row""><span>Total HT</span><span>{f.TotalHt:N3} {f.Devise}</span></div>
      <div class=""total-row""><span>Total TVA</span><span>{f.TotalTva:N3} {f.Devise}</span></div>
      <div class=""total-final""><span>Total TTC</span><span>{f.TotalTtc:N3} {f.Devise}</span></div>
    </div>
  </div>
  <div class=""footer"">
    <div class=""footer-brand"">Généré le {now} | TuniFlow | TEIF {ent.VersionTeif}</div>
    <div style=""font-size:10px;color:#ccc"">Page 1 / 1</div>
  </div>
</div>
</body>
</html>";
    }

    
    
    

    private static Task<FactureDto> MapToDtoAsync(
        Facture f, Client client, CancellationToken _)
    {
        var lignes = f.Lignes.Select(l => new LigneFactureDto(
            l.Id, l.Ordre, l.Designation, l.Description,
            l.Unite, l.Quantite, l.PrixUnitaire,
            l.TauxRemise, l.TauxTva,
            l.MontantHt, l.MontantRemise, l.MontantTva, l.MontantTtc,
            l.ProduitId)).ToList();

        var historique = f.Historique
            .OrderByDescending(h => h.CreeLe)
            .Select(h => new HistoriqueFactureDto(
                h.Id, h.Action, h.Details,
                h.AncienneValeur, h.NouvelleValeur, h.CreeLe))
            .ToList();

        var dto = new FactureDto(
            f.Id, f.EntrepriseId, f.ClientId,
            f.FactureOrigineId,
            client.Nom, client.MatriculeFiscal,
            f.Numero, f.Reference,
            f.Statut.ToString(), f.TypeFacture.ToString(),
            f.ModePaiement.ToString(), f.Devise,
            f.DateEmission, f.DateEcheance, f.DatePaiement,
            f.TotalHt, f.TotalTva, f.TotalTtc,
            f.AppliquerRS, f.CodeRS, f.TauxRS, f.BaseRS, f.MontantRS, f.NetAPayer,
            f.MontantPaye, f.MontantRestant, f.EstEnRetard,
            f.Notes, f.ConditionsPaiement,
            !string.IsNullOrEmpty(f.XmlTeif),
            f.VersionTeif, lignes, historique,
            f.CreeLe, f.ModifieLe);

        return Task.FromResult(dto);
    }
    public async Task<IReadOnlyList<HistoriqueFactureDto>> ObtenirHistoriqueAsync(
    Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(id, ct)
            ?? throw new NotFoundException("Facture introuvable.");
        if (facture.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        return facture.Historique
            .OrderByDescending(h => h.CreeLe)
            .Select(h => new HistoriqueFactureDto(
                h.Id, h.Action, h.Details,
                h.AncienneValeur, h.NouvelleValeur, h.CreeLe))
            .ToList();
    }
}
