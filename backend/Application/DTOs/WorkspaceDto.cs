namespace TodoListApp2.Models.DTOs;

public record WorkspaceDto(
    int Id,
    int? OrganizationId,
    string Name,
    string? Description,
    string Mode,
    string Color,
    int CreatedByUserId,
    int TotalTasks,
    int CompletedTasks,
    int TotalProjects,
    int MemberCount,
    DateTime CreatedAt
);

public record CreateWorkspaceRequest(
    string Name,
    string? Description,
    string Mode = "Personal",
    string Color = "#4f46e5",
    int? OrganizationId = null
);

public record WorkspaceMemberDto(
    int Id,
    int WorkspaceId,
    int UserId,
    string Username,
    string FullName,
    string Email,
    string Role,
    DateTime JoinedAt,
    string? UserRole = null
);

public record AddWorkspaceMemberRequest(
    string UsernameOrEmail,
    string Role = "Editor",
    int? ProjectId = null
);
