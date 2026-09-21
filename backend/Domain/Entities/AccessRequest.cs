namespace TodoListApp2.Models.Entities;

public class AccessRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string RequestedRole { get; set; } = "Employee"; // OrgAdmin, Manager, Employee
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public string? ProposedOrganizationName { get; set; }
    public int? TargetManagerUserId { get; set; }
    public User? TargetManagerUser { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public string? Comments { get; set; }
    public int? DecidedByUserId { get; set; }
    public User? DecidedByUser { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
