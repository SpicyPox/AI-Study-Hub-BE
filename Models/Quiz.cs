using System;
using System.Collections.Generic;

namespace AIStudyHub.Api.Models;

/// <summary>Bộ quiz trắc nghiệm do AI tạo từ nội dung 1 tài liệu.</summary>
public partial class Quiz
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid DocumentId { get; set; }

    public string Title { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Document Document { get; set; } = null!;

    public virtual ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();

    public virtual ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
}
