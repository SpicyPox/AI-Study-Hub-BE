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
public class ConversationsController(AppDbContext db, ClaudeService claude) : ControllerBase
{
    [HttpGet]
    public async Task<ConversationListResponse> GetAll()
    {
        var uid = UserId();
        var convs = await db.Conversations
            .Where(c => c.UserId == uid)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new ConversationDto(c.Id, c.Title, c.DocumentId, c.UpdatedAt))
            .ToListAsync();
        return new ConversationListResponse(convs);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateConversationRequest req)
    {
        var conv = new Conversation
        {
            Title = req.Title ?? "Cuộc trò chuyện mới",
            DocumentId = req.DocumentId,
            UserId = UserId(),
        };
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();
        return Ok(new { conversation = new ConversationDto(conv.Id, conv.Title, conv.DocumentId, conv.UpdatedAt) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var conv = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId())
            ?? throw new KeyNotFoundException("Cuộc trò chuyện không tồn tại.");
        db.Conversations.Remove(conv);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<MessageListResponse> GetMessages(Guid id)
    {
        var uid = UserId();
        _ = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == uid)
            ?? throw new KeyNotFoundException("Cuộc trò chuyện không tồn tại.");

        var msgs = await db.Messages
            .Where(m => m.ConversationId == id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MessageDto(m.Id, m.Role, m.Content, m.TokensUsed, m.CreatedAt))
            .ToListAsync();
        return new MessageListResponse(msgs);
    }

    [HttpPost("{id:guid}/messages")]
    public async Task SendMessage(Guid id, [FromBody] SendMessageRequest req, CancellationToken ct)
    {
        var uid = UserId();
        var conv = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == uid)
            ?? throw new KeyNotFoundException("Cuộc trò chuyện không tồn tại.");

        // Save user message
        var userMsg = new Message { Role = "user", Content = req.Content, ConversationId = id };
        db.Messages.Add(userMsg);
        conv.UpdatedAt = DateTime.UtcNow;
        if (conv.Title == "Cuộc trò chuyện mới" && req.Content.Length > 0)
            conv.Title = req.Content[..Math.Min(40, req.Content.Length)];
        await db.SaveChangesAsync();

        // Get document context if provided
        string? docContext = null;
        if (req.DocumentId.HasValue)
        {
            var doc = await db.Documents.FindAsync(req.DocumentId.Value);
            if (doc is not null)
                docContext = $"Tài liệu: {doc.Name}\nMô tả: {doc.Description ?? "Không có mô tả"}";
        }

        // Stream Claude response
        await claude.StreamAsync(req.Content, docContext, Response, ct);

        // Note: saving the assistant message would require capturing the full streamed response.
        // For a production implementation, use a response capturing wrapper or have the client
        // send a follow-up confirmation. Left as a TODO for the backend team.
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
