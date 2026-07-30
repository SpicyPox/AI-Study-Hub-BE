using System.Security.Claims;
using System.Text.RegularExpressions;
using AIStudyHub.Api.Data;
using AIStudyHub.Api.DTOs.Subjects;
using AIStudyHub.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AIStudyHub.Api.Controllers;

[ApiController]
[Route("api/subjects")]
[Authorize]
public partial class SubjectsController(AppDbContext db) : ControllerBase
{
    // Quy uoc ma mon hoc cua truong: 3 chu cai + 3 chu so, vd SWR302 (Software Requirement).
    // Chi ap dung cho ma MOI tao/doi - khong hoi to cac ma cu (vd "CV", "TEST101") da co san.
    [GeneratedRegex("^[A-Z]{3}[0-9]{3}$")]
    private static partial Regex SubjectCodeFormat();
    [HttpGet]
    public async Task<SubjectListResponse> GetAll()
    {
        var subjects = await db.Subjects
            .Select(s => new SubjectDto(
                s.Id, s.Name, s.Code ?? "", null,
                s.Documents.Count(d => !d.IsDeleted)))
            .ToListAsync();
        return new SubjectListResponse(subjects);
    }

    [HttpPost]
    public async Task<SubjectDto> Create(CreateSubjectRequest req)
    {
        var name = req.Name.Trim();
        var code = req.Code.Trim().ToUpperInvariant();

        // Ma mon hoc (khong Ten) moi la dinh danh duy nhat cua 1 mon hoc: cung 1 Ten co the
        // hop le trung nhau o 2 nganh khac nhau voi Ma khac nhau (vd "Toan cao cap" o
        // TOAN-CNTT va TOAN-KT). Chi khi Ma da ton tai (request bi gui lai/trung lap that su)
        // moi tra ve ban ghi cu thay vi tao moi.
        var existing = await FindByCodeAsync(code);
        if (existing is not null)
            return await ToDto(existing);

        if (!SubjectCodeFormat().IsMatch(code))
            throw new InvalidOperationException("Mã môn học phải đúng định dạng 3 chữ + 3 số, ví dụ: SWR302.");

        var subject = new Subject { Name = name, Code = code, Description = "" };
        db.Subjects.Add(subject);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Rieng cho truong hop 2 request tao cung 1 ma mon hoc gan nhu dong thoi (race
            // condition): request thua se khong bao loi cho nguoi dung, ma tra ve luon ban ghi
            // ma request thang da tao.
            var winner = await FindByCodeAsync(code)
                ?? throw new InvalidOperationException($"Mã môn học '{code}' đã tồn tại.");
            return await ToDto(winner);
        }
        return new SubjectDto(subject.Id, subject.Name, subject.Code ?? "", null, 0);
    }

    private Task<Subject?> FindByCodeAsync(string code) =>
        db.Subjects.FirstOrDefaultAsync(s => s.Code != null && s.Code.ToLower() == code.ToLower());

    private async Task<SubjectDto> ToDto(Subject s)
    {
        var count = await db.Documents.CountAsync(d => d.SubjectId == s.Id && !d.IsDeleted);
        return new SubjectDto(s.Id, s.Name, s.Code ?? "", null, count);
    }

    // Sua/xoa mon hoc anh huong toan bo he thong (master data dung chung), khong phai du lieu
    // rieng cua 1 user - chi Admin duoc phep, khac voi Create/GetAll van mo cho user thuong
    // vi ho can tu tao mon hoc luc upload.
    [Authorize(Roles = "admin")]
    [HttpPatch("{id:guid}")]
    public async Task<SubjectDto> Update(Guid id, UpdateSubjectRequest req)
    {
        var subject = await db.Subjects.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException("Môn học không tồn tại.");

        if (req.Name is not null) subject.Name = req.Name.Trim();
        if (req.Code is not null)
        {
            var code = req.Code.Trim().ToUpperInvariant();
            if (await db.Subjects.AnyAsync(s => s.Id != id && s.Code != null && s.Code.ToLower() == code.ToLower()))
                throw new InvalidOperationException($"Mã môn học '{code}' đã tồn tại.");
            // Chi bat buoc dung dinh dang khi doi sang ma khac - khong hoi to neu admin chi
            // sua Ten ma giu nguyen ma cu (co the la ma theo quy uoc cu, vd "CV").
            if (code != subject.Code && !SubjectCodeFormat().IsMatch(code))
                throw new InvalidOperationException("Mã môn học phải đúng định dạng 3 chữ + 3 số, ví dụ: SWR302.");
            subject.Code = code;
        }

        await db.SaveChangesAsync();
        var count = await db.Documents.CountAsync(d => d.SubjectId == id && !d.IsDeleted);
        return new SubjectDto(subject.Id, subject.Name, subject.Code ?? "", null, count);
    }

    [Authorize(Roles = "admin")]
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
