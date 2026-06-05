using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AIStudyHub.Api.Services;

public class ClaudeService(IConfiguration config, IHttpClientFactory httpFactory)
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private bool IsStub => string.IsNullOrEmpty(config["Anthropic:ApiKey"]) ||
                           config["Anthropic:ApiKey"] == "sk-ant-YOUR_API_KEY_HERE";

    public async Task StreamAsync(
        string userMessage,
        string? documentContext,
        HttpResponse response,
        CancellationToken ct)
    {
        response.Headers.Append("Content-Type", "text/event-stream");
        response.Headers.Append("Cache-Control", "no-cache");
        response.Headers.Append("X-Accel-Buffering", "no");

        if (IsStub)
        {
            await StreamStubAsync(userMessage, response, ct);
            return;
        }

        await StreamClaudeAsync(userMessage, documentContext, response, ct);
    }

    private static async Task StreamStubAsync(string userMessage, HttpResponse response, CancellationToken ct)
    {
        var reply = $"[Stub] Câu trả lời cho: \"{userMessage}\"\n\nĐây là phản hồi demo. Hãy thêm Anthropic API key vào appsettings.json để bật Claude AI thật.";
        foreach (var word in reply.Split(' '))
        {
            var chunk = JsonSerializer.Serialize(new { content = word + " " });
            await response.WriteAsync($"data: {chunk}\n\n", ct);
            await response.Body.FlushAsync(ct);
            await Task.Delay(30, ct);
        }
        await response.WriteAsync("data: [DONE]\n\n", ct);
    }

    private async Task StreamClaudeAsync(
        string userMessage,
        string? documentContext,
        HttpResponse response,
        CancellationToken ct)
    {
        var systemPrompt = documentContext is not null
            ? $"Bạn là trợ lý học tập AI. Hãy trả lời dựa trên nội dung tài liệu sau:\n\n{documentContext}\n\nTrả lời bằng tiếng Việt, súc tích và có trích dẫn trang nếu có."
            : "Bạn là trợ lý học tập AI. Hãy trả lời ngắn gọn, chính xác bằng tiếng Việt.";

        var body = new
        {
            model = "claude-sonnet-4-6",
            max_tokens = 1024,
            stream = true,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userMessage } }
        };

        var http = httpFactory.CreateClient("Anthropic");
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", config["Anthropic:ApiKey"]);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var httpResponse = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        httpResponse.EnsureSuccessStatusCode();

        await using var stream = await httpResponse.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null && !ct.IsCancellationRequested)
        {
            if (!line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") break;

            try
            {
                using var doc = JsonDocument.Parse(data);
                var type = doc.RootElement.GetProperty("type").GetString();
                if (type == "content_block_delta")
                {
                    var delta = doc.RootElement.GetProperty("delta");
                    var text = delta.GetProperty("text").GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        var chunk = JsonSerializer.Serialize(new { content = text });
                        await response.WriteAsync($"data: {chunk}\n\n", ct);
                        await response.Body.FlushAsync(ct);
                    }
                }
                else if (type == "message_stop")
                {
                    await response.WriteAsync("data: [DONE]\n\n", ct);
                    break;
                }
            }
            catch { /* skip malformed chunks */ }
        }
    }
}
