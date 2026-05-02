using System.Globalization;
using System.Text.Json.Nodes;
using Einvoicing.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Einvoicing.Application.Services;

internal static class FacturePdfBuilder
{
    private sealed record PdfSettings(
        string AccentColor,
        string FooterText,
        string? Iban,
        bool ShowIban,
        byte[]? LogoBytes
    );

    public static byte[] Generate(
        Facture facture,
        Client client,
        Entreprise entreprise,
        string? personnalisationJson = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var culture = CultureInfo.GetCultureInfo("fr-TN");
        var settings = BuildSettings(entreprise, personnalisationJson);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Content().Column(col =>
                {
                    col.Spacing(14);
                    col.Item().Element(c => ComposeHeader(c, facture, entreprise, settings));
                    col.Item().Element(c => ComposeParties(c, client, entreprise));
                    col.Item().Element(c => ComposeDates(c, facture));
                    col.Item().Element(c => ComposeTable(c, facture, culture, settings));
                    col.Item().AlignRight().Element(c => ComposeTotals(c, facture, culture, settings));

                    if (HasAdditionalInfo(facture, settings))
                        col.Item().Element(c => ComposeAdditionalInfo(c, facture, settings));
                });

                page.Footer().Element(c => ComposeFooter(c, entreprise, settings));
            });
        });

        return document.GeneratePdf();
    }

    private static PdfSettings BuildSettings(Entreprise entreprise, string? personnalisationJson)
    {
        var accentColor = Colors.Grey.Darken4;
        var footerText = string.Empty;
        string? iban = null;
        var showIban = false;
        var logoBytes = TryExtractImageBytes(entreprise.LogoUrl);

        if (string.IsNullOrWhiteSpace(personnalisationJson))
            return new PdfSettings(accentColor, footerText, iban, showIban, logoBytes);

        try
        {
            var root = JsonNode.Parse(personnalisationJson);
            var pdf = root?["pdf"];
            accentColor = NormalizeColor(GetString(pdf?["primaryColor"]));
            footerText = GetString(pdf?["footerText"])?.Trim() ?? string.Empty;
            iban = GetString(pdf?["iban"])?.Trim();
            showIban = GetBool(pdf?["options"]?["showIban"]);

            var showLogo = GetBool(pdf?["options"]?["showLogo"], true);
            if (showLogo)
            {
                var customLogo = TryExtractImageBytes(GetString(pdf?["logoUrl"]));
                if (customLogo is not null)
                    logoBytes = customLogo;
            }
            else
            {
                logoBytes = null;
            }
        }
        catch
        {
        }

        return new PdfSettings(accentColor, footerText, iban, showIban, logoBytes);
    }

    private static void ComposeHeader(IContainer container, Facture facture, Entreprise entreprise, PdfSettings settings)
    {
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(10).Row(row =>
        {
            row.RelativeItem().Row(left =>
            {
                if (settings.LogoBytes is not null)
                {
                    left.ConstantItem(48).Height(36)
                        .Image(settings.LogoBytes)
                        .FitArea();
                }
                else
                {
                    left.ConstantItem(36).Height(36)
                        .Background(settings.AccentColor)
                        .AlignCenter().AlignMiddle()
                        .Text("TF").FontColor(Colors.White).SemiBold().FontSize(12);
                }

                left.RelativeItem().PaddingLeft(10).Column(col =>
                {
                    col.Item().Text(entreprise.Nom).FontSize(14).SemiBold();
                    col.Item().Text("Portail de facturation electronique certifiee")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });
            });

            row.ConstantItem(200).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text($"{facture.TypeFacture.ToString().ToUpperInvariant()} N? {facture.Numero}")
                    .FontSize(14).SemiBold();
                col.Item().AlignRight().Text(facture.Statut.ToString())
                    .FontSize(9).FontColor(Colors.Grey.Darken2);
            });
        });
    }

    private static void ComposeParties(IContainer container, Client client, Entreprise entreprise)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(c => PartyBox(
                c,
                "Emetteur",
                entreprise.Nom,
                entreprise.Adresse ?? string.Empty,
                entreprise.Email ?? string.Empty,
                entreprise.MatriculeFiscal));

            row.ConstantItem(12);

            row.RelativeItem().Element(c => PartyBox(
                c,
                "Client",
                client.Nom,
                client.Email ?? string.Empty,
                string.Empty,
                client.MatriculeFiscal));
        });
    }

    private static void PartyBox(
        IContainer container,
        string label,
        string nom,
        string ligne1,
        string ligne2,
        string? matriculeFiscal)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
        {
            col.Item().Text(label.ToUpperInvariant())
                .FontSize(8).FontColor(Colors.Grey.Darken2).SemiBold();
            col.Item().Text(nom).FontSize(11).SemiBold();
            if (!string.IsNullOrWhiteSpace(ligne1))
                col.Item().Text(ligne1).FontSize(9).FontColor(Colors.Grey.Darken1);
            if (!string.IsNullOrWhiteSpace(ligne2))
                col.Item().Text(ligne2).FontSize(9).FontColor(Colors.Grey.Darken1);
            if (!string.IsNullOrWhiteSpace(matriculeFiscal))
                col.Item().Text($"MF : {matriculeFiscal}").FontSize(9).FontColor(Colors.Grey.Darken2);
        });
    }

    private static void ComposeDates(IContainer container, Facture facture)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(c => DateBox(c, "Date emission", facture.DateEmission.ToString("dd/MM/yyyy")));
            row.ConstantItem(12);
            row.RelativeItem().Element(c => DateBox(c, "Date echeance", facture.DateEcheance.ToString("dd/MM/yyyy")));
            row.ConstantItem(12);
            row.RelativeItem().Element(c => DateBox(c, "Mode de paiement", facture.ModePaiement.ToString()));
        });
    }

    private static void DateBox(IContainer container, string label, string value)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(col =>
        {
            col.Item().Text(label.ToUpperInvariant())
                .FontSize(8).FontColor(Colors.Grey.Darken2).SemiBold();
            col.Item().Text(value).FontSize(10).SemiBold();
        });
    }

    private static void ComposeTable(IContainer container, Facture facture, CultureInfo culture, PdfSettings settings)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1.4f);
            });

            table.Header(header =>
            {
                header.Cell().Element(c => HeaderCell(c, settings)).Text("Designation");
                header.Cell().Element(c => HeaderCell(c, settings)).AlignCenter().Text("Qte");
                header.Cell().Element(c => HeaderCell(c, settings)).AlignRight().Text("PU HT");
                header.Cell().Element(c => HeaderCell(c, settings)).AlignCenter().Text("Remise");
                header.Cell().Element(c => HeaderCell(c, settings)).AlignCenter().Text("TVA");
                header.Cell().Element(c => HeaderCell(c, settings)).AlignRight().Text("HT");
                header.Cell().Element(c => HeaderCell(c, settings)).AlignRight().Text("TTC");
            });

            foreach (var ligne in facture.Lignes.OrderBy(l => l.Ordre))
            {
                table.Cell().Element(BodyCell).Text(ligne.Designation);
                table.Cell().Element(BodyCell).AlignCenter().Text(ligne.Quantite.ToString("N3", culture));
                table.Cell().Element(BodyCell).AlignRight().Text(ligne.PrixUnitaire.ToString("N3", culture));
                table.Cell().Element(BodyCell).AlignCenter().Text($"{ligne.TauxRemise:N1}%");
                table.Cell().Element(BodyCell).AlignCenter().Text($"{ligne.TauxTva:N0}%");
                table.Cell().Element(BodyCell).AlignRight().Text(ligne.MontantHt.ToString("N3", culture));
                table.Cell().Element(BodyCell).AlignRight().Text(ligne.MontantTtc.ToString("N3", culture));
            }
        });
    }

    private static IContainer HeaderCell(IContainer container, PdfSettings settings)
    {
        return container.Background(settings.AccentColor)
            .PaddingVertical(4).PaddingHorizontal(3)
            .DefaultTextStyle(t => t.FontColor(Colors.White).FontSize(8).SemiBold());
    }

    private static IContainer BodyCell(IContainer container)
    {
        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(3).PaddingHorizontal(3)
            .DefaultTextStyle(t => t.FontSize(9));
    }

    private static void ComposeTotals(IContainer container, Facture facture, CultureInfo culture, PdfSettings settings)
    {
        container.Width(220).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("Total HT");
                row.ConstantItem(110).AlignRight().Text(Money(facture.TotalHt, facture.Devise, culture));
            });
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("Total TVA");
                row.ConstantItem(110).AlignRight().Text(Money(facture.TotalTva, facture.Devise, culture));
            });
            col.Item().Background(settings.AccentColor).Padding(6).Row(row =>
            {
                row.RelativeItem().Text("Total TTC").FontColor(Colors.White).SemiBold();
                row.ConstantItem(110).AlignRight()
                    .Text(Money(facture.TotalTtc, facture.Devise, culture))
                    .FontColor(Colors.White).SemiBold();
            });
        });
    }

    private static bool HasAdditionalInfo(Facture facture, PdfSettings settings)
        => !string.IsNullOrWhiteSpace(facture.ConditionsPaiement)
           || !string.IsNullOrWhiteSpace(facture.Notes)
           || !string.IsNullOrWhiteSpace(facture.Reference)
           || (settings.ShowIban && !string.IsNullOrWhiteSpace(settings.Iban));

    private static void ComposeAdditionalInfo(IContainer container, Facture facture, PdfSettings settings)
    {
        container.PaddingTop(6).Column(col =>
        {
            col.Spacing(8);

            if (!string.IsNullOrWhiteSpace(facture.ConditionsPaiement))
                col.Item().Element(c => InfoBox(c, "Conditions de paiement", facture.ConditionsPaiement!));

            if (!string.IsNullOrWhiteSpace(facture.Notes))
                col.Item().Element(c => InfoBox(c, "Notes", facture.Notes!));

            if (settings.ShowIban && !string.IsNullOrWhiteSpace(settings.Iban))
            {
                var paiement = string.IsNullOrWhiteSpace(facture.Reference)
                    ? $"IBAN : {settings.Iban}"
                    : $"IBAN : {settings.Iban}\nReference : {facture.Reference}";
                col.Item().Element(c => InfoBox(c, "Paiement", paiement));
            }
            else if (!string.IsNullOrWhiteSpace(facture.Reference))
            {
                col.Item().Element(c => InfoBox(c, "Reference", facture.Reference!));
            }
        });
    }

    private static void InfoBox(IContainer container, string label, string value)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
        {
            col.Item().Text(label.ToUpperInvariant())
                .FontSize(8).FontColor(Colors.Grey.Darken2).SemiBold();
            col.Item().PaddingTop(4).Text(value).FontSize(9).FontColor(Colors.Grey.Darken3);
        });
    }

    private static string Money(decimal value, string devise, CultureInfo culture)
        => $"{value.ToString("N3", culture)} {devise}";

    private static void ComposeFooter(IContainer container, Entreprise entreprise, PdfSettings settings)
    {
        var footer = string.IsNullOrWhiteSpace(settings.FooterText)
            ? $"Genere le {DateTime.Now:dd/MM/yyyy HH:mm} | TunisFlow | TEIF {entreprise.VersionTeif}"
            : $"{settings.FooterText} | Genere le {DateTime.Now:dd/MM/yyyy HH:mm}";

        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(footer)
                .FontSize(8).FontColor(Colors.Grey.Darken2);
            row.ConstantItem(90).AlignRight()
                .DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken2))
                .Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
        });
    }

    private static string NormalizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return Colors.Grey.Darken4;

        var trimmed = color.Trim();
        return trimmed.StartsWith("#") && (trimmed.Length == 7 || trimmed.Length == 9)
            ? trimmed
            : Colors.Grey.Darken4;
    }

    private static string? GetString(JsonNode? node)
    {
        try
        {
            return node?.GetValue<string>();
        }
        catch
        {
            return node?.ToString();
        }
    }

    private static bool GetBool(JsonNode? node, bool fallback = false)
    {
        try
        {
            return node?.GetValue<bool>() ?? fallback;
        }
        catch
        {
            var raw = GetString(node);
            return bool.TryParse(raw, out var parsed) ? parsed : fallback;
        }
    }

    private static byte[]? TryExtractImageBytes(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        var trimmed = source.Trim();
        const string marker = "base64,";
        var markerIndex = trimmed.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            var payload = trimmed[(markerIndex + marker.Length)..];
            try
            {
                return Convert.FromBase64String(payload);
            }
            catch
            {
                return null;
            }
        }

        try
        {
            return Convert.FromBase64String(trimmed);
        }
        catch
        {
            return null;
        }
    }
}
