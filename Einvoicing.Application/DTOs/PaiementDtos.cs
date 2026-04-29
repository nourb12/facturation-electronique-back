using Einvoicing.Domain.Enums;

namespace Einvoicing.Application.DTOs;

public record EnregistrerPaiementRequest(
    Guid FactureId,
    decimal Montant,
    ModePaiement Mode,
    DateTime DatePaiement,
    string? Reference,
    string? Banque,
    string? Notes
);

public record PaiementDto(
    Guid Id,
    Guid FactureId,
    string FactureNumero,
    decimal Montant,
    string Devise,
    string Mode,
    string? Reference,
    string? Banque,
    string? Notes,
    DateTime DatePaiement,
    DateTime CreeLe
);

public record ListePaiementsDto(
    List<PaiementDto> Items,
    int Total,
    decimal TotalPaye,
    decimal TotalRestant
);
