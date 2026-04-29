namespace Einvoicing.Application.Options;

public sealed class RelanceOptions
{
    public bool Actif { get; set; } = true;
    public List<int> DelaisJours { get; set; } = new() { 7, 14, 30 };
    public int HeureExecutionUtc { get; set; } = 2;
    public string SujetTemplate { get; set; } = "Relance J+{delai} - Facture {numero}";
    public string TypeNotif { get; set; } = "rappel";
}