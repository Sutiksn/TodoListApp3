namespace TodoListApp2.Models.DTOs;

public record ActivityLogDto(
    int Id,
    int? OrganizationId,
    int? WorkspaceId,
    int? ProjectId,
    int? TaskId,
    int UserId,
    string Username,
    string Action,
    string Details,
    string Mode,
    DateTime Timestamp
);
