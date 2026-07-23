using System;

namespace AIStudyHub.Api.Models;

/// <summary>1 lần user làm xong 1 Quiz — lưu điểm để hiển thị lịch sử.</summary>
public partial class QuizAttempt
{
    public Guid Id { get; set; }

    public Guid QuizId { get; set; }

    public Guid UserId { get; set; }

    public int Score { get; set; }

    public int TotalQuestions { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Quiz Quiz { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
