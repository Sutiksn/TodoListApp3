namespace TodoListApp2.Models.DTOs;

public record GoalDto(
    int Id,
    int WorkspaceId,
    string Title,
    string? Description,
    string Status,
    int ProgressPercentage,
    DateTime? TargetDate,
    int TotalProjects,
    DateTime CreatedAt
);

public record CreateGoalRequest(
    int WorkspaceId,
    string Title,
    string? Description,
    DateTime? TargetDate,
    string Status = "InProgress"
);

public record ProjectDto(
    int Id,
    int WorkspaceId,
    int? GoalId,
    string? GoalTitle,
    string Name,
    string? Description,
    string Color,
    string Status,
    string Priority,
    DateTime? StartDate,
    DateTime? EndDate,
    int ProgressPercentage,
    int TotalTasks,
    int CompletedTasks,
    int OpenTasks,
    int MilestoneCount,
    DateTime CreatedAt,
    int CreatedByUserId = 0
);

public record CreateProjectRequest(
    int WorkspaceId,
    string Name,
    string? Description,
    string Color = "#6366f1",
    string Priority = "Medium",
    int? GoalId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
);

public record MilestoneDto(
    int Id,
    int ProjectId,
    string Title,
    string? Description,
    DateTime? DueDate,
    bool IsReached,
    DateTime? ReachedAt
);

public record CreateMilestoneRequest(
    int ProjectId,
    string Title,
    string? Description,
    DateTime? DueDate
);
