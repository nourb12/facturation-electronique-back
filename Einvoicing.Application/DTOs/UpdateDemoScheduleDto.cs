namespace Einvoicing.Application.DTOs;

/// <summary>Créneau mis à jour par l'équipe pour une demande de démo.</summary>
public class UpdateDemoScheduleDto
{
    public DateTime PreferredDate { get; set; }
    public string PreferredTime { get; set; } = string.Empty;
}
