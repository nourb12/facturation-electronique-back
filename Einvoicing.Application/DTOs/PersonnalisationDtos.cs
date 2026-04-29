using System.Text.Json.Nodes;

namespace Einvoicing.Application.DTOs;

public record EnregistrerPersonnalisationRequest(
    JsonNode? Donnees
);

public record PersonnalisationDto(
    Guid Id,
    Guid EntrepriseId,
    JsonNode Donnees,
    DateTime CreeLe,
    DateTime ModifieLe
);