namespace AIStudyHub.Api.DTOs.Subjects;

public record CreateSubjectRequest(string Name, string Code, string Color);
public record UpdateSubjectRequest(string? Name, string? Code, string? Color);

public record SubjectDto(Guid Id, string Name, string Code, string Color, int Count);
public record SubjectListResponse(IEnumerable<SubjectDto> Subjects);
