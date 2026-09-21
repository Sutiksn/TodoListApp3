namespace TodoListApp2.Models.DTOs;

public record AccessRequestDto(
    int Id,
    int UserId,
    string Username,
    string FullName,
    string Email,
    string RequestedRole,
    int? OrganizationId,
    string? OrganizationName,
    string? ProposedOrganizationName,
    int? TargetManagerUserId,
    string? TargetManagerName,
    string Status,
    string? Comments,
    int? DecidedByUserId,
    string? DecidedByUsername,
    DateTime? DecidedAt,
    DateTime CreatedAt
);

public record CreateAccessRequestDto(
    string RequestedRole, // OrgAdmin, Manager, Employee
    int? OrganizationId = null,
    int? TargetManagerUserId = null,
    string? OrganizationName = null,
    string? Comments = null
);

public record DecideAccessRequestDto(
    string? Action = null, // Approve, Reject
    bool? Approved = null,
    string? Comments = null
);
