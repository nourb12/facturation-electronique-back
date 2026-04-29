using System.Globalization;
using Einvoicing.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Einvoicing.Application.Services;

internal static class FacturePdfBuilder
{
    public static byte[] Generate(Facture facture, Client client, Entreprise entreprise)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var culture = CultureInfo.GetCultureInfo("fr-TN");

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
                    col.Item().Element(c => ComposeHeader(c, facture, entreprise));
                    col.Item().Element(c => ComposeParties(c, client, entreprise));
                    col.Item().Element(c => ComposeDates(c, facture));
                    col.Item().Element(c => ComposeTable(c, facture, culture));
                    col.Item().AlignRight().Element(c => ComposeTotals(c, facture, culture));
                });

                page.Footer().Element(c => ComposeFooter(c, entreprise));
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, Facture facture, Entreprise entreprise)
    {
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(10).Row(row =>
        {
            row.RelativeItem().Row(left =>
            {
                left.ConstantItem(36).Height(36)
                    .Background(Colors.Grey.Darken4)
                    .AlignCenter().AlignMiddle()
                    .Text("TF").FontColor(Colors.White).SemiBold().FontSize(12);

                left.RelativeItem().PaddingLeft(10).Column(col =>
                {
                    col.Item().Text(entreprise.Nom).FontSize(14).SemiBold();
                    col.Item().Text("Portail de facturation electronique certifiee")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });
            });

            row.ConstantItem(200).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text($"FACTURE N° {facture.Numero}")
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

    private static void ComposeTable(IContainer container, Facture facture, CultureInfo culture)
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
                header.Cell().Element(HeaderCell).Text("Designation");
                header.Cell().Element(HeaderCell).AlignCenter().Text("Qte");
                header.Cell().Element(HeaderCell).AlignRight().Text("PU HT");
                header.Cell().Element(HeaderCell).AlignCenter().Text("Remise");
                header.Cell().Element(HeaderCell).AlignCenter().Text("TVA");
                header.Cell().Element(HeaderCell).AlignRight().Text("HT");
                header.Cell().Element(HeaderCell).AlignRight().Text("TTC");
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

    private static IContainer HeaderCell(IContainer container)
    {
        return container.Background(Colors.Grey.Darken4)
            .PaddingVertical(4).PaddingHorizontal(3)
            .DefaultTextStyle(t => t.FontColor(Colors.White).FontSize(8).SemiBold());
    }

    private static IContainer BodyCell(IContainer container)
    {
        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(3).PaddingHorizontal(3)
            .DefaultTextStyle(t => t.FontSize(9));
    }

    private static void ComposeTotals(IContainer container, Facture facture, CultureInfo culture)
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
            col.Item().Background(Colors.Grey.Darken4).Padding(6).Row(row =>
            {
                row.RelativeItem().Text("Total TTC").FontColor(Colors.White).SemiBold();
                row.ConstantItem(110).AlignRight()
                    .Text(Money(facture.TotalTtc, facture.Devise, culture))
                    .FontColor(Colors.White).SemiBold();
            });
        });
    }

    private static string Money(decimal value, string devise, CultureInfo culture)
        => $"{value.ToString("N3", culture)} {devise}";

    private static void ComposeFooter(IContainer container, Entreprise entreprise)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text($"Genere le {DateTime.Now:dd/MM/yyyy HH:mm} | TunisFlow | TEIF {entreprise.VersionTeif}")
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
}