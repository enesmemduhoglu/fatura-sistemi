using System.Globalization;
using Isbasi.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Isbasi.Web.Data;

/// <summary>
/// Fatura PDF çıktısı (QuestPDF): Print görünümüyle aynı içerik — şablon tema rengi,
/// stopaj/tevkifat satırları, dövizli belgede TL karşılığı ve fatura dipnotu.
/// Sayılar tr-TR biçimindedir; ₺ gibi simgeler için Segoe UI önceliklidir.
/// </summary>
public static class InvoicePdfGenerator
{
    // Üretici nereden çağrılırsa çağrılsın (test dahil) lisans ayarlı olmalı
    static InvoicePdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly CultureInfo Tr = new("tr-TR");

    public static byte[] Generate(Invoice invoice, CompanySettings? company)
    {
        // Sade şablonda vurgu gri; geçersiz renk değerleri varsayılana düşer (Print ile aynı kural)
        string hex = company?.PrintTemplate == "plain" ? "#333333" : (company?.PrintAccentColor ?? "#e8112d");
        if (!System.Text.RegularExpressions.Regex.IsMatch(hex, "^#[0-9a-fA-F]{6}$")) hex = "#e8112d";
        var accent = Color.FromHex(hex);

        string sym = invoice.CurrencySymbol;
        string typeTitle = invoice.Type switch
        {
            InvoiceType.SalesWholesale => "SATIŞ FATURASI",
            InvoiceType.SalesRetail => "SATIŞ FATURASI",
            InvoiceType.Expense => "GİDER FATURASI",
            InvoiceType.SalesOrder => "SATIŞ SİPARİŞİ",
            InvoiceType.PurchaseOrder => "ALIŞ SİPARİŞİ",
            InvoiceType.SalesReturn => "SATIŞ İADE FATURASI",
            InvoiceType.PurchaseReturn => "ALIŞ İADE FATURASI",
            _ => "ALIŞ FATURASI"
        };
        string partyTitle = invoice.IsSales || invoice.Type == InvoiceType.SalesReturn
            ? "SAYIN (ALICI)" : "SATICI";

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Segoe UI", "Lato"));

                // Başlık: firma bilgileri + belge adı/numarası
                page.Header().PaddingBottom(10).BorderBottom(2).BorderColor(accent).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(company?.CompanyName ?? "Firmam").FontSize(16).Bold().FontColor(accent);
                        if (!string.IsNullOrWhiteSpace(company?.OwnerName)) col.Item().Text(company.OwnerName);
                        if (!string.IsNullOrWhiteSpace(company?.Address)) col.Item().Text(company.Address);
                        if (!string.IsNullOrWhiteSpace(company?.City))
                            col.Item().Text(string.IsNullOrWhiteSpace(company.District)
                                ? company.City : $"{company.District} / {company.City}");
                        if (!string.IsNullOrWhiteSpace(company?.TaxNumber))
                            col.Item().Text($"Vergi No: {company.TaxNumber}");
                    });
                    row.ConstantItem(210).Column(col =>
                    {
                        col.Item().AlignRight().Text(typeTitle).FontSize(13).Bold();
                        col.Item().AlignRight().Text(t =>
                        {
                            t.Span("Fatura No: ");
                            t.Span(invoice.InvoiceNumber).SemiBold();
                        });
                    });
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    // Taraflar, teslimat adresi ve tarih bilgileri
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(p =>
                        {
                            p.Item().Text(partyTitle).FontSize(7).FontColor(Colors.Grey.Darken1);
                            p.Item().Text(invoice.Firm?.Name ?? "-").FontSize(11).SemiBold();
                            if (!string.IsNullOrWhiteSpace(invoice.Firm?.Address)) p.Item().Text(invoice.Firm.Address);
                            if (!string.IsNullOrWhiteSpace(invoice.Firm?.City))
                                p.Item().Text(string.IsNullOrWhiteSpace(invoice.Firm.District)
                                    ? invoice.Firm.City : $"{invoice.Firm.District} / {invoice.Firm.City}");
                            if (!string.IsNullOrWhiteSpace(invoice.Firm?.TaxOffice))
                                p.Item().Text($"Vergi Dairesi: {invoice.Firm.TaxOffice}");
                            if (!string.IsNullOrWhiteSpace(invoice.Firm?.TaxNumber))
                                p.Item().Text($"Vergi No: {invoice.Firm.TaxNumber}");
                        });
                        if (!string.IsNullOrWhiteSpace(invoice.DeliveryAddress))
                        {
                            row.RelativeItem().PaddingHorizontal(8).Column(p =>
                            {
                                p.Item().Text("TESLİMAT ADRESİ").FontSize(7).FontColor(Colors.Grey.Darken1);
                                p.Item().Text(invoice.DeliveryAddress);
                            });
                        }
                        row.ConstantItem(190).Column(d =>
                        {
                            void DateLine(string label, string value, bool bold = false)
                                => d.Item().AlignRight().Text(t =>
                                {
                                    t.Span($"{label}: ").FontColor(Colors.Grey.Darken1);
                                    var span = t.Span(value);
                                    if (bold) span.SemiBold();
                                });

                            DateLine("Fatura Tarihi", invoice.InvoiceDate.ToString("dd.MM.yyyy"), bold: true);
                            if (invoice.ShipmentDate.HasValue)
                                DateLine("Sevk Tarihi", invoice.ShipmentDate.Value.ToString("dd.MM.yyyy"));
                            if (invoice.DueDate.HasValue)
                                DateLine("Vade Tarihi", invoice.DueDate.Value.ToString("dd.MM.yyyy"));
                            if (!string.IsNullOrWhiteSpace(invoice.DeliveryNoteNumber))
                                DateLine("İrsaliye No", invoice.DeliveryNoteNumber);
                            if (invoice.DeliveryNoteDate.HasValue)
                                DateLine("İrsaliye Tarihi", invoice.DeliveryNoteDate.Value.ToString("dd.MM.yyyy"));
                            DateLine("Döviz", invoice.Currency);
                            if (invoice.Currency != "TL")
                                DateLine("Kur", invoice.ExchangeRate.ToString("N4", Tr) + " ₺");
                        });
                    });

                    // Satırlar
                    col.Item().PaddingTop(14).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(22);     // #
                            c.RelativeColumn(4);      // ürün/hizmet
                            c.RelativeColumn(1.2f);   // miktar
                            c.RelativeColumn(1);      // birim
                            c.RelativeColumn(1.6f);   // birim fiyat
                            c.RelativeColumn(1);      // KDV %
                            c.RelativeColumn(1.5f);   // KDV tutarı
                            c.RelativeColumn(1.7f);   // tutar
                        });

                        IContainer Head(IContainer c) => c
                            .BorderBottom(1.5f).BorderColor(accent)
                            .PaddingVertical(4).PaddingHorizontal(2);
                        IContainer Cell(IContainer c) => c
                            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .PaddingVertical(3).PaddingHorizontal(2);

                        table.Header(h =>
                        {
                            h.Cell().Element(Head).Text("#").SemiBold();
                            h.Cell().Element(Head).Text("Ürün / Hizmet").SemiBold();
                            h.Cell().Element(Head).AlignRight().Text("Miktar").SemiBold();
                            h.Cell().Element(Head).Text("Birim").SemiBold();
                            h.Cell().Element(Head).AlignRight().Text("Birim Fiyat").SemiBold();
                            h.Cell().Element(Head).AlignRight().Text("KDV %").SemiBold();
                            h.Cell().Element(Head).AlignRight().Text("KDV Tutarı").SemiBold();
                            h.Cell().Element(Head).AlignRight().Text("Tutar").SemiBold();
                        });

                        for (int i = 0; i < invoice.Lines.Count; i++)
                        {
                            var line = invoice.Lines[i];
                            table.Cell().Element(Cell).Text((i + 1).ToString());
                            table.Cell().Element(Cell).Text(line.ItemName);
                            table.Cell().Element(Cell).AlignRight().Text(line.Quantity.ToString("0.##", Tr));
                            table.Cell().Element(Cell).Text(line.Unit);
                            table.Cell().Element(Cell).AlignRight().Text(line.UnitPrice.ToString("N2", Tr));
                            table.Cell().Element(Cell).AlignRight().Text("%" + line.VatRate.ToString("0", Tr));
                            table.Cell().Element(Cell).AlignRight().Text(line.VatAmount.ToString("N2", Tr));
                            table.Cell().Element(Cell).AlignRight().Text(line.LineTotal.ToString("N2", Tr));
                        }
                    });

                    // Toplamlar (Print görünümüyle aynı satırlar ve koşullar)
                    col.Item().PaddingTop(10).AlignRight().Width(260).Column(t =>
                    {
                        void TotalLine(string label, string value, bool grand = false)
                            => t.Item().BorderBottom(grand ? 0 : 0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Background(grand ? accent : Colors.White)
                                .PaddingVertical(grand ? 5 : 3).PaddingHorizontal(grand ? 6 : 2)
                                .Row(r =>
                                {
                                    var left = r.RelativeItem().Text(label);
                                    var right = r.ConstantItem(110).AlignRight().Text(value);
                                    if (grand)
                                    {
                                        left.SemiBold().FontColor(Colors.White);
                                        right.SemiBold().FontColor(Colors.White);
                                    }
                                });

                        TotalLine("Ara Toplam", $"{invoice.SubTotal.ToString("N2", Tr)} {sym}");
                        if (invoice.DiscountTotal > 0)
                            TotalLine("Toplam İndirim", $"-{invoice.DiscountTotal.ToString("N2", Tr)} {sym}");
                        if (invoice.StopajTotal > 0)
                            TotalLine($"Stopaj (%{invoice.StopajRate.ToString("0.##", Tr)})",
                                $"-{invoice.StopajTotal.ToString("N2", Tr)} {sym}");
                        TotalLine("Toplam KDV", $"{invoice.VatTotal.ToString("N2", Tr)} {sym}");
                        if (invoice.TevkifatVatTotal > 0)
                            TotalLine($"KDV Tevkifatı ({invoice.TevkifatCode})",
                                $"-{invoice.TevkifatVatTotal.ToString("N2", Tr)} {sym}");
                        TotalLine("Genel Toplam", $"{invoice.GrandTotal.ToString("N2", Tr)} {sym}", grand: true);
                        if (invoice.Currency != "TL")
                            TotalLine($"TL Karşılığı (kur {invoice.ExchangeRate.ToString("N4", Tr)})",
                                $"{invoice.GrandTotalTry.ToString("N2", Tr)} ₺");
                    });

                    if (!string.IsNullOrWhiteSpace(invoice.Description))
                        col.Item().PaddingTop(12).Text(t =>
                        {
                            t.Span("Açıklama: ").SemiBold();
                            t.Span(invoice.Description);
                        });

                    if (!string.IsNullOrWhiteSpace(company?.PrintFooterNote))
                        col.Item().PaddingTop(10).Text(company.PrintFooterNote)
                            .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7).FontColor(Colors.Grey.Medium));
                    t.Span($"{invoice.InvoiceNumber}  |  {DateTime.Now.ToString("dd.MM.yyyy HH:mm", Tr)}  |  Sayfa ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}
