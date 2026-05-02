using System.ComponentModel.DataAnnotations;

namespace Einvoicing.Application.DTOs;

public class UpdateStatusDto
{
    [Required(ErrorMessage = "Le statut est requis")]
    public string Status { get; set; } = string.Empty;
}
