using AIStudyHub.Api.Data;
using AIStudyHub.Api.DTOs.Contact;
using AIStudyHub.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Api.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactController(AppDbContext db) : ControllerBase
{
    // Cong khai: khach chua dang nhap van gui duoc yeu cau lien he tu trang /contact.
    [HttpPost]
    public async Task<ContactMessageDto> Create(CreateContactMessageRequest req)
    {
        var msg = new ContactMessage
        {
            Name = req.Name.Trim(),
            Email = req.Email.Trim().ToLower(),
            Subject = req.Subject.Trim(),
            Message = req.Message.Trim(),
        };
        db.ContactMessages.Add(msg);
        await db.SaveChangesAsync();

        return ToDto(msg);
    }

    [Authorize(Roles = "admin")]
    [HttpGet]
    public async Task<ContactMessageListResponse> GetAll()
    {
        var messages = await db.ContactMessages
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var unread = messages.Count(m => !m.IsRead);
        return new ContactMessageListResponse(messages.Select(ToDto), messages.Count, unread);
    }

    [Authorize(Roles = "admin")]
    [HttpPatch("{id:guid}/read")]
    public async Task<ContactMessageDto> MarkRead(Guid id)
    {
        var msg = await db.ContactMessages.FindAsync(id)
            ?? throw new KeyNotFoundException("Yêu cầu liên hệ không tồn tại.");

        msg.IsRead = true;
        await db.SaveChangesAsync();
        return ToDto(msg);
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var msg = await db.ContactMessages.FindAsync(id)
            ?? throw new KeyNotFoundException("Yêu cầu liên hệ không tồn tại.");

        db.ContactMessages.Remove(msg);
        await db.SaveChangesAsync();
        return Ok();
    }

    private static ContactMessageDto ToDto(ContactMessage m) =>
        new(m.Id, m.Name, m.Email, m.Subject, m.Message, m.IsRead, m.CreatedAt);
}
