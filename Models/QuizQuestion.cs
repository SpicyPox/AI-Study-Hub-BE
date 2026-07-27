using System;

namespace AIStudyHub.Api.Models;

/// <summary>1 câu hỏi trắc nghiệm (4 lựa chọn A-D) thuộc về 1 Quiz.</summary>
public partial class QuizQuestion
{
    public Guid Id { get; set; }

    public Guid QuizId { get; set; }

    public string QuestionText { get; set; } = null!;

    public string OptionA { get; set; } = null!;

    public string OptionB { get; set; } = null!;

    public string OptionC { get; set; } = null!;

    public string OptionD { get; set; } = null!;

    /// <summary>Chỉ số đáp án đúng (0=A, 1=B, 2=C, 3=D).</summary>
    public int CorrectIndex { get; set; }

    public string? Explanation { get; set; }

    public int OrderIndex { get; set; }

    public virtual Quiz Quiz { get; set; } = null!;
}
