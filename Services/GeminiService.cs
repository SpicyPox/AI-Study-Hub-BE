using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIStudyHub.Api.Models;

namespace AIStudyHub.Api.Services;

public class GeminiService(IConfiguration config, IHttpClientFactory httpFactory)
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private bool IsStub => string.IsNullOrEmpty(config["Gemini:ApiKey"]) ||
                           config["Gemini:ApiKey"] == "YOUR_GEMINI_API_KEY_HERE";

    private record GeminiPart(string Text);
    private record GeminiContent(string Role, List<GeminiPart> Parts);

    /// <summary>Ket qua stream: noi dung tra loi + so token AI da dung (0 neu khong lay duoc).</summary>
    public record GeminiStreamResult(string Reply, int Tokens);

    public async Task<GeminiStreamResult> StreamAsync(
        string userMessage,
        IEnumerable<ChatMessage> history,
        string? documentContext,
        HttpResponse response,
        CancellationToken ct)
    {
        response.Headers.Append("Content-Type", "text/event-stream");
        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("X-Accel-Buffering", "no");

        if (IsStub)
        {
            return await StreamStubAsync(userMessage, response, ct);
        }

        return await StreamGeminiAsync(userMessage, history, documentContext, response, ct);
    }

    private static async Task<GeminiStreamResult> StreamStubAsync(string userMessage, HttpResponse response, CancellationToken ct)
    {
        var reply = $"[Stub] Câu trả lời cho: \"{userMessage}\"\n\nĐây là phản hồi demo của Gemini. Hãy thêm Gemini API key vào appsettings.json để bật Gemini AI thật.";
        foreach (var word in reply.Split(' '))
        {
            var chunk = JsonSerializer.Serialize(new { content = word + " " });
            await response.WriteAsync($"data: {chunk}\n\n", ct);
            await response.Body.FlushAsync(ct);
            await Task.Delay(30, ct);
        }
        await response.WriteAsync("data: [DONE]\n\n", ct);
        // Che do stub khong co token that -> uoc luong ~4 ky tu/token de dashboard co so lieu.
        var estTokens = (userMessage.Length + reply.Length) / 4;
        return new GeminiStreamResult(reply, estTokens);
    }

    private async Task<GeminiStreamResult> StreamGeminiAsync(
        string userMessage,
        IEnumerable<ChatMessage> history,
        string? documentContext,
        HttpResponse response,
        CancellationToken ct)
    {
        var systemPrompt = documentContext is not null
            ? $"Bạn là trợ lý học tập AI. Hãy trả lời dựa trên nội dung tài liệu sau:\n\n{documentContext}\n\nTrả lời bằng tiếng Việt, súc tích và có trích dẫn trang nếu có."
            : "Bạn là trợ lý học tập AI. Hãy trả lời ngắn gọn, chính xác bằng tiếng Việt.";

        var rawContents = new List<GeminiContent>();
        foreach (var msg in history)
        {
            var role = msg.Role == ChatRole.assistant ? "model" : "user";
            rawContents.Add(new GeminiContent(role, new List<GeminiPart> { new GeminiPart(msg.Content) }));
        }
        rawContents.Add(new GeminiContent("user", new List<GeminiPart> { new GeminiPart(userMessage) }));

        var sanitizedContents = SanitizeContents(rawContents);

        var body = new
        {
            contents = sanitizedContents,
            systemInstruction = new
            {
                parts = new[] { new GeminiPart(systemPrompt) }
            },
            generationConfig = new
            {
                maxOutputTokens = 1024
            }
        };

        var http = httpFactory.CreateClient("Gemini");
        var apiKey = config["Gemini:ApiKey"];
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse&key={apiKey}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json")
        };

        using var httpResponse = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        httpResponse.EnsureSuccessStatusCode();

        await using var stream = await httpResponse.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var fullReplyBuilder = new StringBuilder();
        int totalTokens = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null && !ct.IsCancellationRequested)
        {
            if (!line.StartsWith("data: ")) continue;
            var data = line[6..];

            try
            {
                using var doc = JsonDocument.Parse(data);
                if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        var text = parts[0].GetProperty("text").GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            fullReplyBuilder.Append(text);
                            var chunk = JsonSerializer.Serialize(new { content = text });
                            await response.WriteAsync($"data: {chunk}\n\n", ct);
                            await response.Body.FlushAsync(ct);
                        }
                    }
                }

                // usageMetadata xuat hien o chunk cuoi (tong cong don) -> lay lam so token that.
                if (doc.RootElement.TryGetProperty("usageMetadata", out var usage) &&
                    usage.TryGetProperty("totalTokenCount", out var ttc) &&
                    ttc.TryGetInt32(out var t))
                {
                    totalTokens = t;
                }
            }
            catch { /* skip malformed chunks */ }
        }

        await response.WriteAsync("data: [DONE]\n\n", ct);
        return new GeminiStreamResult(fullReplyBuilder.ToString(), totalTokens);
    }

    public async Task<string> GenerateSummaryAsync(string documentContent, CancellationToken ct)
    {
        if (IsStub)
        {
            return "[Stub] Đây là bản tóm tắt của tài liệu. Hãy thêm Gemini API key vào appsettings.json để nhận tóm tắt từ Gemini AI thật.";
        }

        var systemPrompt = "Bạn là một chuyên gia tóm tắt tài liệu. Hãy tóm tắt tài liệu dưới đây bằng tiếng Việt, ngắn gọn, súc tích, làm nổi bật các ý chính bằng gạch đầu dòng.";
        var body = new
        {
            contents = new[]
            {
                new GeminiContent("user", new List<GeminiPart> { new GeminiPart(documentContent) })
            },
            systemInstruction = new
            {
                parts = new[] { new GeminiPart(systemPrompt) }
            },
            generationConfig = new
            {
                maxOutputTokens = 1536
            }
        };

        var http = httpFactory.CreateClient("Gemini");
        var apiKey = config["Gemini:ApiKey"];
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json")
        };

        using var httpResponse = await http.SendAsync(request, ct);
        httpResponse.EnsureSuccessStatusCode();

        var jsonStr = await httpResponse.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(jsonStr);
        if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var firstCandidate = candidates[0];
            if (firstCandidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0)
            {
                var text = parts[0].GetProperty("text").GetString();
                return text ?? string.Empty;
            }
        }

        return string.Empty;
    }

    /// <summary>1 câu hỏi trắc nghiệm 4 lựa chọn do Gemini sinh ra, dùng để build QuizQuestion.</summary>
    public record GeneratedQuizQuestion(string Question, List<string> Options, int CorrectIndex, string? Explanation);

    public async Task<List<GeneratedQuizQuestion>> GenerateQuizAsync(string documentContent, int questionCount, CancellationToken ct)
    {
        if (IsStub)
        {
            return Enumerable.Range(1, questionCount).Select(i => new GeneratedQuizQuestion(
                $"[Stub] Câu hỏi mẫu số {i} về tài liệu này?",
                new List<string> { "Đáp án A", "Đáp án B", "Đáp án C", "Đáp án D" },
                0,
                "Đây là quiz demo. Hãy thêm Gemini API key vào appsettings.json để AI sinh câu hỏi thật từ nội dung tài liệu."
            )).ToList();
        }

        var systemPrompt =
            $"Bạn là một giáo viên tạo đề trắc nghiệm. Dựa trên nội dung tài liệu dưới đây, hãy tạo đúng {questionCount} câu hỏi " +
            "trắc nghiệm 4 lựa chọn (A/B/C/D) bằng tiếng Việt để kiểm tra mức độ hiểu bài của học viên. " +
            "Mỗi câu chỉ có 1 đáp án đúng. Trả lời CHỈ bằng JSON hợp lệ theo đúng schema, không thêm chữ nào khác.";

        var body = new
        {
            contents = new[]
            {
                new GeminiContent("user", new List<GeminiPart> { new GeminiPart(documentContent) })
            },
            systemInstruction = new
            {
                parts = new[] { new GeminiPart(systemPrompt) }
            },
            generationConfig = new
            {
                maxOutputTokens = 4096,
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        questions = new
                        {
                            type = "ARRAY",
                            items = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    question = new { type = "STRING" },
                                    options = new { type = "ARRAY", items = new { type = "STRING" }, minItems = "4", maxItems = "4" },
                                    correctIndex = new { type = "INTEGER" },
                                    explanation = new { type = "STRING" },
                                },
                                required = new[] { "question", "options", "correctIndex" },
                            },
                        },
                    },
                    required = new[] { "questions" },
                },
            },
        };

        var http = httpFactory.CreateClient("Gemini");
        var apiKey = config["Gemini:ApiKey"];
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
        var bodyJson = JsonSerializer.Serialize(body, _json);

        var jsonStr = await SendWithRetryAsync(http, url, bodyJson, ct);
        using var doc = JsonDocument.Parse(jsonStr);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("AI không trả về nội dung quiz hợp lệ.");

        using var quizDoc = JsonDocument.Parse(text);
        var result = new List<GeneratedQuizQuestion>();
        foreach (var q in quizDoc.RootElement.GetProperty("questions").EnumerateArray())
        {
            var options = q.GetProperty("options").EnumerateArray().Select(o => o.GetString() ?? "").ToList();
            if (options.Count != 4) continue; // bo qua cau hoi AI tra sai schema (thieu/thua luachon)

            result.Add(new GeneratedQuizQuestion(
                q.GetProperty("question").GetString() ?? "",
                options,
                q.GetProperty("correctIndex").GetInt32(),
                q.TryGetProperty("explanation", out var expl) ? expl.GetString() : null
            ));
        }

        if (result.Count == 0)
            throw new InvalidOperationException("AI không tạo được câu hỏi nào từ tài liệu này.");

        return result;
    }

    /// <summary>Gui request toi Gemini, tu dong thu lai khi gap 429/503 (qua tai tam thoi phia Google).</summary>
    private static async Task<string> SendWithRetryAsync(HttpClient http, string url, string bodyJson, CancellationToken ct)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
            };
            using var httpResponse = await http.SendAsync(request, ct);

            var isRetryable = httpResponse.StatusCode is System.Net.HttpStatusCode.ServiceUnavailable
                or System.Net.HttpStatusCode.TooManyRequests;

            if (httpResponse.IsSuccessStatusCode)
                return await httpResponse.Content.ReadAsStringAsync(ct);

            if (!isRetryable || attempt == maxAttempts)
            {
                httpResponse.EnsureSuccessStatusCode();
            }

            await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
        }

        throw new InvalidOperationException("Gemini API không phản hồi sau nhiều lần thử lại.");
    }

    private static List<GeminiContent> SanitizeContents(List<GeminiContent> rawContents)
    {
        var sanitized = new List<GeminiContent>();
        string? lastRole = null;
        var mergedText = new StringBuilder();

        foreach (var c in rawContents)
        {
            var text = c.Parts.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(text)) continue;

            var currentRole = c.Role.ToLower() == "model" ? "model" : "user";

            if (lastRole == currentRole)
            {
                mergedText.AppendLine().Append(text);
            }
            else
            {
                if (lastRole != null)
                {
                    sanitized.Add(new GeminiContent(lastRole, new List<GeminiPart> { new GeminiPart(mergedText.ToString()) }));
                }
                lastRole = currentRole;
                mergedText.Clear().Append(text);
            }
        }

        if (lastRole != null)
        {
            sanitized.Add(new GeminiContent(lastRole, new List<GeminiPart> { new GeminiPart(mergedText.ToString()) }));
        }

        // Make sure it starts with "user"
        while (sanitized.Count > 0 && sanitized[0].Role != "user")
        {
            sanitized.RemoveAt(0);
        }

        return sanitized;
    }
}
