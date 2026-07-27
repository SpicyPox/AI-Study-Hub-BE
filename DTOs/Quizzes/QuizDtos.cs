using System.ComponentModel.DataAnnotations;

namespace AIStudyHub.Api.DTOs.Quizzes;

public record CreateQuizRequest(
    [Required] Guid DocumentId,
    [Range(3, 15)] int QuestionCount = 5
);

public record QuizQuestionDto(
    Guid Id, string QuestionText, string[] Options,
    int CorrectIndex, string? Explanation, int OrderIndex
);

public record QuizDto(
    Guid Id, string Title, Guid DocumentId, string DocumentName,
    DateTime CreatedAt, List<QuizQuestionDto> Questions,
    int? BestScore, int AttemptCount
);

// Dùng cho danh sách lịch sử — không kèm câu hỏi để nhẹ payload.
public record QuizSummaryDto(
    Guid Id, string Title, Guid DocumentId, string DocumentName,
    int QuestionCount, DateTime CreatedAt, int? BestScore, int AttemptCount
);

public record QuizListResponse(IEnumerable<QuizSummaryDto> Quizzes);

public record SubmitAttemptRequest(
    [Range(0, int.MaxValue)] int Score,
    [Range(1, int.MaxValue)] int TotalQuestions
);

public record QuizAttemptDto(Guid Id, int Score, int TotalQuestions, DateTime CreatedAt);
