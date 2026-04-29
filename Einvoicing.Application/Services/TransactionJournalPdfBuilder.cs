using System.Globalization;
using Einvoicing.Application.DTOs;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Einvoicing.Application.Services;

internal static class TransactionJournalPdfBuilder
{
    public static byte[] Generate(Entreprise entreprise, IReadOnlyList<Transaction> items, FiltreTransactionsRequest filtre)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var culture = CultureInfo.GetCultureInfo("fr-TN");

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(9).FontColor(Colors.Grey.Darken4));

                page.Content().Column(col =>
                {
                    col.Spacing(12);
                    col.Item().Element(c => ComposeHeader(c, entreprise, filtre));
                    col.Item().Element(c => ComposeKpis(c, items, culture));
                    col.Item().Element(c => ComposeTable(c, items, culture));
                });

                page.Footer().Element(c => ComposeFooter(c, entreprise));
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, Entreprise entreprise, FiltreTransactionsRequest filtre)
    {
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(10).Row(row =>
        {
            row.RelativeItem().Row(left =>
            {
                left.ConstantItem(34).Height(34)
                    .Background(Colors.Grey.Darken4)
                    .AlignCenter().AlignMiddle()
                    .Text("TF").FontColor(Colors.White).SemiBold().FontSize(12);

                left.RelativeItem().PaddingLeft(10).Column(col =>
                {
                    col.Item().Text(entreprise.Nom).FontSize(13).SemiBold();
                    col.Item().Text("Journal des transactions")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });
            });

            row.ConstantItem(240).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text("JOURNAL TRANSACTIONS")
                    .FontSize(13).SemiBold();

                var periode = BuildPeriodeLabel(filtre);
                col.Item().AlignRight().Text(periode)
                    .FontSize(8).FontColor(Colors.Grey.Darken2);
            });
        });
    }

    private static string BuildPeriodeLabel(FiltreTransactionsRequest filtre)
    {
        var deb = filtre.DateDebut?.ToString("dd/MM/yyyy") ?? "—";
        var fin = filtre.DateFin?.ToString("dd/MM/yyyy") ?? "—";
        return $"Période : {deb} → {fin}";
    }

    private static void ComposeKpis(IContainer container, IReadOnlyList<Transaction> items, CultureInfo culture)
    {
        var non = items.Where(x => x.Statut == StatutTransaction.NonJustifiee).ToList();
        var att = items.Where(x => x.Statut == StatutTransaction.EnAttente).ToList();
        var jus = items.Where(x => x.Statut == StatutTransaction.Justifiee).ToList();

        container.Row(row =>
        {
            row.RelativeItem().Element(c => KpiCard(c, "NON JUSTIFIÉ", non.Count, SumSigned(non), culture, Colors.Red.Medium));
            row.ConstantItem(10);
            row.RelativeItem().Element(c => KpiCard(c, "EN ATTENTE", att.Count, SumSigned(att), culture, Colors.Orange.Medium));
            row.ConstantItem(10);
            row.RelativeItem().Element(c => KpiCard(c, "JUSTIFIÉ", jus.Count, SumSigned(jus), culture, Colors.Green.Medium));
        });
    }

    private static decimal SumSigned(IReadOnlyList<Transaction> items)
    {
        decimal sum = 0;
        foreach (var t in items)
            sum += t.Type == TypeTransaction.Sortie ? -t.Montant : t.Montant;
        return sum;
    }

    private static void KpiCard(
        IContainer container,
        string label,
        int count,
        decimal montant,
        CultureInfo culture,
        string accentColor)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
        {
            col.Item().Row(r =>
            {
                r.ConstantItem(6).Height(6).Background(accentColor);
                r.ConstantItem(8);
                r.RelativeItem().Text(label).FontSize(8).SemiBold().FontColor(Colors.Grey.Darken2);
            });

            col.Item().PaddingTop(6).Text($"{count} — {montant.ToString("N3", culture)} TND")
                .FontSize(11).SemiBold();
        });
    }

    private static void ComposeTable(IContainer container, IReadOnlyList<Transaction> items, CultureInfo culture)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(70);     // Date
                columns.RelativeColumn(3.2f);   // Libellé
                columns.RelativeColumn(2.2f);   // Tiers
                columns.RelativeColumn(1.8f);   // Catégorie
                columns.ConstantColumn(55);     // Type
                columns.ConstantColumn(85);     // Montant
                columns.ConstantColumn(85);     // Statut
            });

            table.Header(h =>
            {
                h.Cell().Element(HeaderCell).Text("DATE");
                h.Cell().Element(HeaderCell).Text("LIBELLÉ");
                h.Cell().Element(HeaderCell).Text("TIERS");
                h.Cell().Element(HeaderCell).Text("CATÉGORIE");
                h.Cell().Element(HeaderCell).AlignCenter().Text("TYPE");
                h.Cell().Element(HeaderCell).AlignRight().Text("MONTANT");
                h.Cell().Element(HeaderCell).AlignCenter().Text("STATUT");
            });

            foreach (var t in items.OrderByDescending(x => x.Date))
            {
                table.Cell().Element(BodyCell).Text(t.Date.ToString("dd/MM/yyyy"));
                table.Cell().Element(BodyCell).Text(t.Libelle);
                table.Cell().Element(BodyCell).Text(t.TiersNom ?? "—").FontColor(Colors.Grey.Darken2);
                table.Cell().Element(BodyCell).Text(t.CategorieNom ?? "—").FontColor(Colors.Grey.Darken2);

                table.Cell().Element(BodyCell).AlignCenter().Text(t.Type.ToString().ToUpperInvariant()).FontSize(8).SemiBold();

                var signed = t.Type == TypeTransaction.Sortie ? -t.Montant : t.Montant;
                table.Cell().Element(BodyCell).AlignRight()
                    .Text($"{signed.ToString("N3", culture)} {t.Devise}")
                    .FontFamily("Helvetica")
                    .FontSize(9)
                    .SemiBold()
                    .FontColor(signed < 0 ? Colors.Red.Medium : Colors.Green.Medium);

                table.Cell().Element(BodyCell).AlignCenter().Text(t.Statut.ToString()).FontSize(8).SemiBold()
                    .FontColor(StatutColor(t.Statut));
            }
        });
    }

    private static string StatutColor(StatutTransaction statut)
    {
        return statut switch
        {
            StatutTransaction.Justifiee => Colors.Green.Medium,
            StatutTransaction.EnAttente => Colors.Orange.Medium,
            _ => Colors.Red.Medium
        };
    }

    private static IContainer HeaderCell(IContainer c)
    {
        return c.Background(Colors.Grey.Darken4)
            .PaddingVertical(5).PaddingHorizontal(4)
            .DefaultTextStyle(t => t.FontColor(Colors.White).FontSize(8).SemiBold());
    }

    private static IContainer BodyCell(IContainer c)
    {
        return c.BorderBottom(1).BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(4).PaddingHorizontal(4)
            .DefaultTextStyle(t => t.FontSize(9));
    }

    private static void ComposeFooter(IContainer container, Entreprise entreprise)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text($"Généré le {DateTime.Now:dd/MM/yyyy HH:mm} | TunisFlow | TEIF {entreprise.VersionTeif}")
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
