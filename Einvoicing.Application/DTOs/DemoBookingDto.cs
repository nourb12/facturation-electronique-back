namespace Einvoicing.Application.DTOs;

/// <summary>
/// DTO pour la réservation d'une session de démonstration EY-Factify
/// </summary>
public class DemoBookingDto
{
    public string Prenom { get; set; } = "";
    public string Nom { get; set; } = "";
    public string Email { get; set; } = "";
    public string Entreprise { get; set; } = "";
    public string? Telephone { get; set; }
    public string? Message { get; set; }
    public DateTime Date { get; set; }
    public string Heure { get; set; } = "";
}
