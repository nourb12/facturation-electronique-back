namespace Einvoicing.Application.DTOs;

public sealed record CashForecastRequestDto(
    decimal? SoldeInitial = null,
    int HorizonSemaines = 8,
    decimal TauxRecouvrement = 0.85m);

public sealed record CashForecastInvoiceDto(
    Guid FactureId,
    string Numero,
    string Client,
    DateTime DateEcheance,
    decimal Montant,
    decimal ProbabiliteRetard,
    decimal MontantAPayerAEcheance,
    decimal MontantReporteAttendu,
    int DelaiEstimeJours,
    DateTime DatePaiementEstimee);

public sealed record CashForecastWeekDto(
    DateTime DebutSemaine,
    string LibelleSemaine,
    decimal EncaissementsOptimistes,
    decimal EncaissementsAEcheance,
    decimal EncaissementsReportes,
    decimal EncaissementsRealistes,
    decimal Decaissements,
    decimal SoldeOptimiste,
    decimal SoldeRealiste,
    decimal SoldePessimiste);

public sealed record CashForecastSummaryDto(
    decimal SoldeInitial,
    decimal TotalFactures,
    decimal TotalDecaissements,
    decimal TotalReporteAttendu,
    decimal FinOptimiste,
    decimal FinRealiste,
    decimal FinPessimiste,
    string Alerte);

public sealed record CashForecastDto(
    string Source,
    decimal TauxRecouvrement,
    string RegleDecalage,
    CashForecastSummaryDto Resume,
    IReadOnlyList<CashForecastWeekDto> Semaines,
    IReadOnlyList<CashForecastInvoiceDto> Factures);
