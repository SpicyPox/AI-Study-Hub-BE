using System.Text;
using AIStudyHub.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using UglyToad.PdfPig;

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

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var fileType = (document.FileType ?? string.Empty).ToLower();

            if (fileType == "pdf")
            {
                text = ExtractTextFromPdf(stream);
            }
            else if (fileType == "txt" || fileType == "md" || fileType == "json")
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                text = await reader.ReadToEndAsync(ct);
            }
            else
            {
                // Fallback for unsupported formats
                text = $"[Tài liệu không phải dạng văn bản thuần túy hoặc PDF. Định dạng: {fileType}]\n" +
                       $"Tiêu đề: {document.Title}\nMô tả: {document.Description ?? "Không có mô tả"}";
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
}
