using System.Text;
using AIStudyHub.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;
using D = DocumentFormat.OpenXml.Drawing;

namespace AIStudyHub.Api.Services;

public class DocumentTextExtractor(IHttpClientFactory httpFactory, IMemoryCache cache)
{
    public async Task<string> ExtractTextAsync(Document document, CancellationToken ct = default)
    {
        if (document.CloudFile == null || string.IsNullOrWhiteSpace(document.CloudFile.CloudUrl))
        {
            return string.Empty;
        }

        var cacheKey = $"doc_text_{document.Id}";
        if (cache.TryGetValue(cacheKey, out string? cachedText) && cachedText is not null)
        {
            return cachedText;
        }

        string text = string.Empty;
        try
        {
            var client = httpFactory.CreateClient();
            var response = await client.GetAsync(document.CloudFile.CloudUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var fileType = (document.FileType ?? string.Empty).ToLower();

            if (fileType == "txt" || fileType == "md" || fileType == "json")
            {
                await using var rawStream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(rawStream, Encoding.UTF8);
                text = await reader.ReadToEndAsync(ct);
            }
            else
            {
                // Các định dạng bên dưới (PDF/DOCX/PPTX/XLSX) đều là file zip/binary, cần stream
                // seek được để parse -> tải hẳn vào MemoryStream trước (stream HTTP gốc không seek được).
                using var buffer = new MemoryStream();
                await response.Content.CopyToAsync(buffer, ct);
                buffer.Position = 0;

                text = fileType switch
                {
                    "pdf" => ExtractTextFromPdf(buffer),
                    "docx" => ExtractTextFromDocx(buffer),
                    "pptx" => ExtractTextFromPptx(buffer),
                    "xlsx" => ExtractTextFromXlsx(buffer),
                    // .doc/.ppt/.xls (định dạng binary cũ) và ảnh chưa hỗ trợ trích xuất text thật.
                    _ => $"[Tài liệu không phải dạng văn bản thuần túy hoặc PDF/DOCX/PPTX/XLSX. Định dạng: {fileType}]\n" +
                         $"Tiêu đề: {document.Title}\nMô tả: {document.Description ?? "Không có mô tả"}",
                };
            }
        }
        catch (Exception ex)
        {
            text = $"[Lỗi khi đọc tài liệu: {ex.Message}]";
        }

        // Cache the extracted text for 30 minutes
        cache.Set(cacheKey, text, TimeSpan.FromMinutes(30));

        return text;
    }

    private static string ExtractTextFromPdf(Stream stream)
    {
        try
        {
            using var pdfDoc = PdfDocument.Open(stream);
            var sb = new StringBuilder();
            foreach (var page in pdfDoc.GetPages())
            {
                var pageText = page.Text;
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    sb.AppendLine(pageText);
                }
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"[Lỗi khi phân tích tệp PDF: {ex.Message}]";
        }
    }

    private static string ExtractTextFromDocx(Stream stream)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(stream, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body is null) return string.Empty;

            var sb = new StringBuilder();
            foreach (var para in body.Descendants<W.Paragraph>())
            {
                var paraText = para.InnerText;
                if (!string.IsNullOrWhiteSpace(paraText)) sb.AppendLine(paraText);
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"[Lỗi khi phân tích tệp DOCX: {ex.Message}]";
        }
    }

    private static string ExtractTextFromPptx(Stream stream)
    {
        try
        {
            using var doc = PresentationDocument.Open(stream, false);
            var slideParts = doc.PresentationPart?.SlideParts;
            if (slideParts is null) return string.Empty;

            var sb = new StringBuilder();
            var slideNo = 1;
            foreach (var slidePart in slideParts)
            {
                var texts = (slidePart.Slide?.Descendants<D.Text>() ?? [])
                    .Select(t => t.Text)
                    .Where(t => !string.IsNullOrWhiteSpace(t));
                var slideText = string.Join(" ", texts);
                if (!string.IsNullOrWhiteSpace(slideText))
                    sb.AppendLine($"Slide {slideNo}: {slideText}");
                slideNo++;
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"[Lỗi khi phân tích tệp PPTX: {ex.Message}]";
        }
    }

    private static string ExtractTextFromXlsx(Stream stream)
    {
        try
        {
            using var doc = SpreadsheetDocument.Open(stream, false);
            var workbookPart = doc.WorkbookPart;
            if (workbookPart is null) return string.Empty;

            var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable?
                .Elements<SharedStringItem>().Select(s => s.InnerText).ToList() ?? [];

            var sb = new StringBuilder();
            foreach (var sheet in workbookPart.Workbook?.Descendants<Sheet>() ?? [])
            {
                if (sheet.Id?.Value is null) continue;
                var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id.Value);
                sb.AppendLine($"--- Sheet: {sheet.Name} ---");

                foreach (var row in worksheetPart.Worksheet?.Descendants<Row>() ?? [])
                {
                    var cellValues = row.Elements<Cell>().Select(cell =>
                    {
                        var raw = cell.CellValue?.InnerText ?? string.Empty;
                        if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(raw, out var idx)
                            && idx >= 0 && idx < sharedStrings.Count)
                        {
                            return sharedStrings[idx];
                        }
                        return raw;
                    }).Where(v => !string.IsNullOrWhiteSpace(v));

                    var rowText = string.Join(" | ", cellValues);
                    if (!string.IsNullOrWhiteSpace(rowText)) sb.AppendLine(rowText);
                }
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"[Lỗi khi phân tích tệp XLSX: {ex.Message}]";
        }
    }
}
