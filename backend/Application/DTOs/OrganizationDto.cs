namespace TodoListApp2.Models.DTOs;

public record OrganizationDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    string? LogoUrl,
    string SubscriptionPlan,
    int MaxMembers,
    int MemberCount,
    int WorkspaceCount,
    int TotalTasks,
    DateTime CreatedAt
);

public record CreateOrganizationRequest(
    string Name,
    string? Description = null,
    string SubscriptionPlan = "Enterprise"
);

public record OrgMemberDto(
    int Id,
    int OrganizationId,
    int UserId,
    string Username,
    string FullName,
    string Email,
    string Role,
    string? Department,
    string Status,
    int ActiveTasks,
    DateTime JoinedAt
);

public record AddOrgMemberRequest(
    string UsernameOrEmail,
    string Role = "Member",
    string? Department = "General"
);

public record UpdateOrgMemberRequest(
    string? Role = null,
    string? Department = null,
    string? Status = null
);

public record UserSummaryDto(
    int Id,
    string Username,
    string FullName,
    string Email,
    string Role,
    string? Department
);

public record OrgWithManagersDto(
    int Id,
    string Name,
    string? Description,
    List<UserSummaryDto> Managers
);
