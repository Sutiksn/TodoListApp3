using Microsoft.EntityFrameworkCore;
using TodoListApp2.Models.Entities;

namespace TodoListApp2.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Subtask> Subtasks => Set<Subtask>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();
    public DbSet<WorkflowApproval> WorkflowApprovals => Set<WorkflowApproval>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.Role).HasMaxLength(50).IsRequired();
        });

        // 2. Organization
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(150).IsRequired();
        });

        // 3. OrganizationMember
        modelBuilder.Entity<OrganizationMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrganizationId, e.UserId }).IsUnique();

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Members)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.OrganizationMemberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 4. Workspace
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Mode).HasMaxLength(50).IsRequired();

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Workspaces)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 5. WorkspaceMember
        modelBuilder.Entity<WorkspaceMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.WorkspaceId, e.UserId }).IsUnique();

            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Members)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.WorkspaceMemberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 6. Goal
        modelBuilder.Entity<Goal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();

            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Goals)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 7. Project
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();

            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Projects)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Goal)
                .WithMany(g => g.Projects)
                .HasForeignKey(e => e.GoalId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 8. Milestone
        modelBuilder.Entity<Milestone>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();

            entity.HasOne(e => e.Project)
                .WithMany(p => p.Milestones)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 9. TaskItem
        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Priority).HasMaxLength(50).IsRequired();

            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Tasks)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.CreatedByUser)
                .WithMany(u => u.CreatedTasks)
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AssigneeUser)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(e => e.AssigneeUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 10. Subtask
        modelBuilder.Entity<Subtask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(255).IsRequired();

            entity.HasOne(e => e.TaskItem)
                .WithMany(t => t.Subtasks)
                .HasForeignKey(e => e.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 11. TaskComment
        modelBuilder.Entity<TaskComment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.TaskItem)
                .WithMany(t => t.Comments)
                .HasForeignKey(e => e.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 12. TaskAttachment
        modelBuilder.Entity<TaskAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.TaskItem)
                .WithMany(t => t.Attachments)
                .HasForeignKey(e => e.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 13. TaskDependency
        modelBuilder.Entity<TaskDependency>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.TaskItem)
                .WithMany(t => t.Dependencies)
                .HasForeignKey(e => e.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.DependsOnTask)
                .WithMany()
                .HasForeignKey(e => e.DependsOnTaskId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 14. WorkflowApproval
        modelBuilder.Entity<WorkflowApproval>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.TaskItem)
                .WithMany(t => t.Approvals)
                .HasForeignKey(e => e.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.RequesterUser)
                .WithMany()
                .HasForeignKey(e => e.RequesterUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ApproverUser)
                .WithMany()
                .HasForeignKey(e => e.ApproverUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 15. Notification
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 16. ActivityLog
        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Mode);
            entity.HasIndex(e => e.UserId);
        });

        // 17. AccessRequest
        modelBuilder.Entity<AccessRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequestedRole).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.TargetManagerUser)
                .WithMany()
                .HasForeignKey(e => e.TargetManagerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.DecidedByUser)
                .WithMany()
                .HasForeignKey(e => e.DecidedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
