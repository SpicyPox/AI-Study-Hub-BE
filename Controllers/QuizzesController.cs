using System.Security.Claims;
using AIStudyHub.Api.Data;
using AIStudyHub.Api.DTOs.Quizzes;
using AIStudyHub.Api.Models;
using AIStudyHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIStudyHub.Api.Controllers;

[ApiController]
[Route("api/quizzes")]
[Authorize]
public class QuizzesController(AppDbContext db, GeminiService gemini, DocumentTextExtractor extractor) : ControllerBase
{
    [HttpGet]
    public async Task<QuizListResponse> GetAll()
    {
        var uid = UserId();
        var quizzes = await db.Quizzes
            .Include(q => q.Document)
            .Include(q => q.Questions)
            .Include(q => q.Attempts.Where(a => a.UserId == uid))
            .Where(q => q.UserId == uid)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        var dtos = quizzes.Select(q => new QuizSummaryDto(
            q.Id, q.Title, q.DocumentId, q.Document.Title,
            q.Questions.Count, q.CreatedAt,
            q.Attempts.Count > 0 ? q.Attempts.Max(a => a.Score) : null,
            q.Attempts.Count
        ));
        return new QuizListResponse(dtos);
    }

    [HttpGet("{id:guid}")]
    public async Task<QuizDto> GetById(Guid id)
    {
        var uid = UserId();
        var quiz = await db.Quizzes
            .Include(q => q.Document)
            .Include(q => q.Questions.OrderBy(qq => qq.OrderIndex))
            .Include(q => q.Attempts.Where(a => a.UserId == uid))
            .FirstOrDefaultAsync(q => q.Id == id && q.UserId == uid)
            ?? throw new KeyNotFoundException("Quiz không tồn tại.");

        return ToDto(quiz);
    }

    [HttpPost]
    public async Task<QuizDto> Create(CreateQuizRequest req, CancellationToken ct)
    {
        var uid = UserId();

        var hasActiveSubscription = await db.UserSubscriptions
            .AnyAsync(s => s.UserId == uid && s.Status == "active" && s.EndDate > DateTime.UtcNow, ct);
        if (!hasActiveSubscription)
        {
            var quizzesToday = await db.Quizzes
                .CountAsync(q => q.UserId == uid && q.CreatedAt.Date == DateTime.UtcNow.Date, ct);
            if (quizzesToday >= 1)
                throw new InvalidOperationException(
                    "Bạn đã dùng hết lượt tạo quiz miễn phí hôm nay (1 quiz/ngày). Nâng cấp gói Pro để tạo không giới hạn.");
        }

        var doc = await db.Documents.Include(d => d.CloudFile)
            .FirstOrDefaultAsync(d => d.Id == req.DocumentId && !d.IsDeleted
                && (d.Visibility == DocVisibility.@public || d.UserId == uid), ct)
            ?? throw new KeyNotFoundException("Tài liệu không tồn tại.");

        var text = await extractor.ExtractTextAsync(doc, ct);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Không thể trích xuất nội dung văn bản từ tài liệu này để tạo quiz.");

        var generated = await gemini.GenerateQuizAsync(text, req.QuestionCount, ct);

        var quiz = new Quiz
        {
            UserId = uid,
            DocumentId = doc.Id,
            Title = $"Quiz: {doc.Title}",
        };
        for (var i = 0; i < generated.Count; i++)
        {
            var q = generated[i];
            quiz.Questions.Add(new QuizQuestion
            {
                QuestionText = q.Question,
                OptionA = q.Options.ElementAtOrDefault(0) ?? "",
                OptionB = q.Options.ElementAtOrDefault(1) ?? "",
                OptionC = q.Options.ElementAtOrDefault(2) ?? "",
                OptionD = q.Options.ElementAtOrDefault(3) ?? "",
                CorrectIndex = Math.Clamp(q.CorrectIndex, 0, 3),
                Explanation = q.Explanation,
                OrderIndex = i,
            });
        }

        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync(ct);

        quiz.Document = doc;
        return ToDto(quiz);
    }

    [HttpPost("{id:guid}/attempts")]
    public async Task<QuizAttemptDto> SubmitAttempt(Guid id, SubmitAttemptRequest req)
    {
        var uid = UserId();
        var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.Id == id && q.UserId == uid)
            ?? throw new KeyNotFoundException("Quiz không tồn tại.");

        var attempt = new QuizAttempt
        {
            QuizId = quiz.Id,
            UserId = uid,
            Score = Math.Min(req.Score, req.TotalQuestions),
            TotalQuestions = req.TotalQuestions,
        };
        db.QuizAttempts.Add(attempt);
        await db.SaveChangesAsync();

        return new QuizAttemptDto(attempt.Id, attempt.Score, attempt.TotalQuestions, attempt.CreatedAt);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var uid = UserId();
        var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.Id == id && q.UserId == uid)
            ?? throw new KeyNotFoundException("Quiz không tồn tại.");

        db.Quizzes.Remove(quiz);
        await db.SaveChangesAsync();
        return Ok();
    }

    private static QuizDto ToDto(Quiz q) => new(
        q.Id, q.Title, q.DocumentId, q.Document.Title, q.CreatedAt,
        q.Questions.OrderBy(qq => qq.OrderIndex).Select(qq => new QuizQuestionDto(
            qq.Id, qq.QuestionText, new[] { qq.OptionA, qq.OptionB, qq.OptionC, qq.OptionD },
            qq.CorrectIndex, qq.Explanation, qq.OrderIndex
        )).ToList(),
        q.Attempts.Count > 0 ? q.Attempts.Max(a => a.Score) : null,
        q.Attempts.Count
    );

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
