using System.Security.Claims;
using AIStudyHub.Api.Data;
using AIStudyHub.Api.DTOs.Subjects;
using AIStudyHub.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Api.Controllers;

[ApiController]
[Route("api/subjects")]
[Authorize]
public class SubjectsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<SubjectListResponse> GetAll()
    {
        var uid = UserId();
        var subjects = await db.Subjects
            .Select(s => new SubjectDto(
                s.Id, s.Name, s.Code, null, // Color removed
                s.Documents.Count(d => !d.IsDeleted)))
            .ToListAsync();
        return new SubjectListResponse(subjects);
    }

    [HttpPost]
    public async Task<SubjectDto> Create(CreateSubjectRequest req)
    {
        var subject = new Subject
        {
            Name = req.Name, Code = req.Code, Description = ""
        };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();
        return new SubjectDto(subject.Id, subject.Name, subject.Code, null, 0);
    }

    [HttpPatch("{id:guid}")]
    public async Task<SubjectDto> Update(Guid id, UpdateSubjectRequest req)
    {
        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException("Môn học không tồn tại.");

        if (req.Name is not null) subject.Name = req.Name;
        if (req.Code is not null) subject.Code = req.Code;

        await db.SaveChangesAsync();
        var count = await db.Documents.CountAsync(d => d.SubjectId == id && !d.IsDeleted);
        return new SubjectDto(subject.Id, subject.Name, subject.Code, null, count);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException("Môn học không tồn tại.");
        db.Subjects.Remove(subject);
        await db.SaveChangesAsync();
        return Ok();
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
