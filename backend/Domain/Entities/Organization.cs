namespace TodoListApp2.Models.Entities;

public class Organization
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string SubscriptionPlan { get; set; } = "Enterprise"; // Free, Starter, Business, Enterprise
    public int MaxMembers { get; set; } = 50;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
    public ICollection<Workspace> Workspaces { get; set; } = new List<Workspace>();
}

public class OrganizationMember
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Role { get; set; } = "Employee"; // OrgAdmin, Manager, Employee, Member
    public string? Department { get; set; } // Engineering, Product, Marketing, Sales, Operations, HR
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Active"; // Active, Pending, Inactive
}
