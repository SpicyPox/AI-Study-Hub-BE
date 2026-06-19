using System.Security.Claims;
using AIStudyHub.Api.Data;
using AIStudyHub.Api.DTOs.Chat;
using AIStudyHub.Api.Models;
using AIStudyHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController(AppDbContext db, GeminiService gemini, DocumentTextExtractor extractor) : ControllerBase
{
    [HttpGet]
    public async Task<ConversationListResponse> GetAll()
    {
        var uid = UserId();
        var convs = await db.ChatSessions
            .Where(c => c.UserId == uid)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new ConversationDto(c.Id, c.Title, c.Documents.Select(d => (Guid?)d.Id).FirstOrDefault(), c.UpdatedAt))
            .ToListAsync();
        return new ConversationListResponse(convs);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateConversationRequest req)
    {
        var conv = new ChatSession
        {
            Title = req.Title ?? "Cuộc trò chuyện mới",
            UserId = UserId(),
        };
        if (req.DocumentId.HasValue) 
        {
            var doc = await db.Documents.FindAsync(req.DocumentId.Value);
            if (doc != null) conv.Documents.Add(doc);
        }
        db.ChatSessions.Add(conv);
        await db.SaveChangesAsync();
        return Ok(new { conversation = new ConversationDto(conv.Id, conv.Title, req.DocumentId, conv.UpdatedAt) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var conv = await db.ChatSessions.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId())
            ?? throw new KeyNotFoundException("Cuộc trò chuyện không tồn tại.");
        db.ChatSessions.Remove(conv);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<MessageListResponse> GetMessages(Guid id)
    {
        var uid = UserId();
        _ = await db.ChatSessions.FirstOrDefaultAsync(c => c.Id == id && c.UserId == uid)
            ?? throw new KeyNotFoundException("Cuộc trò chuyện không tồn tại.");

        var msgs = await db.ChatMessages
            .Where(m => m.SessionId == id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MessageDto(m.Id, m.Role.ToString(), m.Content, 0, m.CreatedAt))
            .ToListAsync();
        return new MessageListResponse(msgs);
    }

    [HttpPost("{id:guid}/messages")]
    public async Task SendMessage(Guid id, [FromBody] SendMessageRequest req, CancellationToken ct)
    {
        var uid = UserId();
        var conv = await db.ChatSessions.FirstOrDefaultAsync(c => c.Id == id && c.UserId == uid, ct)
            ?? throw new KeyNotFoundException("Cuộc trò chuyện không tồn tại.");

        // Fetch history before saving current user message
        var history = await db.ChatMessages
            .Where(m => m.SessionId == id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        // Save user message
        var userMsg = new ChatMessage { Role = ChatRole.user, Content = req.Content, SessionId = id };
        db.ChatMessages.Add(userMsg);
        conv.UpdatedAt = DateTime.UtcNow;
        if (conv.Title == "Cuộc trò chuyện mới" && req.Content.Length > 0)
            conv.Title = req.Content[..Math.Min(40, req.Content.Length)];
        await db.SaveChangesAsync(ct);

        // Get document context if provided or linked to conversation
        var docId = req.DocumentId;
        if (!docId.HasValue)
        {
            await db.Entry(conv).Collection(c => c.Documents).LoadAsync(ct);
            docId = conv.Documents.Select(d => (Guid?)d.Id).FirstOrDefault();
        }

        string? docContext = null;
        if (docId.HasValue)
        {
            var doc = await db.Documents
                .Include(d => d.CloudFile)
                .FirstOrDefaultAsync(d => d.Id == docId.Value, ct);
            if (doc is not null)
            {
                var extractedText = await extractor.ExtractTextAsync(doc, ct);
                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    docContext = $"Tài liệu: {doc.Title}\nNội dung:\n{extractedText}";
                }
                else
                {
                    docContext = $"Tài liệu: {doc.Title}\nMô tả: {doc.Description ?? "Không có mô tả"}";
                }
            }
        }

        // Stream Gemini response
        var assistantReply = await gemini.StreamAsync(req.Content, history, docContext, Response, ct);

        // Save assistant message
        if (!string.IsNullOrWhiteSpace(assistantReply))
        {
            var assistantMsg = new ChatMessage
            {
                Role = ChatRole.assistant,
                Content = assistantReply,
                SessionId = id
            };
            db.ChatMessages.Add(assistantMsg);
            conv.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
