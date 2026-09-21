namespace TodoListApp2.Models.DTOs;

public record PersonalAnalyticsDto(
    int TotalTasks,
    int PlanCount,
    int ToDoCount,
    int InProgressCount,
    int ReviewCount,
    int CompletedCount,
    int OverdueCount,
    int CompletionRatePercentage,
    int TotalGoals,
    int AchievedGoals,
    double TotalHoursEstimated,
    double TotalHoursSpent
);

public record MemberWorkloadDto(
    int UserId,
    string Username,
    string FullName,
    string? Department,
    int ActiveTasks,
    int CompletedTasks,
    int OverdueTasks,
    string Role = "Member"
);

public record TeamAnalyticsDto(
    int TotalMembers,
    int TotalWorkspaces,
    int TotalTasks,
    int CompletedTasks,
    int PendingReviews,
    int OverdueTasks,
    List<MemberWorkloadDto> MemberWorkloads
);

public record ProjectAnalyticsDto(
    int TotalProjects,
    int ActiveProjects,
    int CompletedProjects,
    int TotalMilestones,
    int ReachedMilestones,
    int OverallProjectProgressPercentage
);

public record DepartmentStatsDto(
    string Department,
    int EmployeeCount,
    int TotalTasks,
    int CompletedTasks,
    int OverdueTasks
);

public record BusinessAnalyticsDto(
    int TotalOrganizations,
    int TotalEmployees,
    int TotalDepartments,
    int TotalWorkspaces,
    int TotalProjects,
    int TotalTasks,
    int PendingApprovals,
    int CompletedTasks,
    List<DepartmentStatsDto> DepartmentBreakdown
);

public record AdminPlatformAnalyticsDto(
    int TotalUsers,
    int TotalOrganizations,
    int TotalWorkspaces,
    int TotalProjects,
    int TotalTasks,
    int TotalApprovals,
    int TotalAuditLogs,
    int ActiveUsersToday,
    string SystemStatus,
    string DbProvider
);
