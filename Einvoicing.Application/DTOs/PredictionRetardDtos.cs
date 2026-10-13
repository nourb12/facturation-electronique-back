namespace Einvoicing.Application.DTOs;

public sealed record PredictionRetardDto(
    Guid FactureId,
    string Prediction,
    decimal ProbabiliteRetard,
    string NiveauRisque,
    int NombreFacturesPrecedentes,
    int NombreRetardsPrecedents);

public sealed record PredictionRetardFactureDto(
    Guid FactureId,
    string Numero,
    Guid ClientId,
    string ClientNom,
    string TypeClient,
    DateTime DateEcheance,
    decimal MontantTtc,
    string Devise,
    string Prediction,
    decimal ProbabiliteRetard,
    string NiveauRisque,
    int NombreFacturesPrecedentes,
    int NombreRetardsPrecedents);
