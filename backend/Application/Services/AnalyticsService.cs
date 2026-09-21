using Microsoft.EntityFrameworkCore;
using TodoListApp2.Data;
using TodoListApp2.Models.DTOs;

namespace TodoListApp2.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _context;

    public AnalyticsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PersonalAnalyticsDto> GetPersonalAnalyticsAsync(int userId)
    {
        var tasks = await _context.Tasks
            .Where(t => t.CreatedByUserId == userId || t.AssigneeUserId == userId)
            .ToListAsync();

        int total = tasks.Count;
        int plan = tasks.Count(t => t.Status == "Plan");
        int todo = tasks.Count(t => t.Status == "ToDo");
        int inProg = tasks.Count(t => t.Status == "InProgress");
        int review = tasks.Count(t => t.Status == "Review");
        int completed = tasks.Count(t => t.Status == "Completed");
        int overdue = tasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Status != "Completed");
        int rate = total > 0 ? (int)Math.Round((double)completed / total * 100) : 0;

        int totalGoals = await _context.Goals.CountAsync(g => g.CreatedByUserId == userId);
        int achievedGoals = await _context.Goals.CountAsync(g => g.CreatedByUserId == userId && g.Status == "Achieved");

        double est = tasks.Sum(t => t.EstimatedHours);
        double spent = tasks.Sum(t => t.ActualHours);

        return new PersonalAnalyticsDto(total, plan, todo, inProg, review, completed, overdue, rate, totalGoals, achievedGoals, est, spent);
    }

    public async Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(int workspaceId)
    {
        var members = await _context.WorkspaceMembers
            .AsNoTracking()
            .Include(m => m.User)
                .ThenInclude(u => u.AssignedTasks)
            .Where(m => m.WorkspaceId == workspaceId && m.User.Role != "PlatformAdmin")
            .ToListAsync();

        var tasks = await _context.Tasks
            .Where(t => t.WorkspaceId == workspaceId)
            .ToListAsync();

        int totalTasks = tasks.Count;
        int completedTasks = tasks.Count(t => t.Status == "Completed");
        int pendingReviews = tasks.Count(t => t.Status == "Review");
        int overdue = tasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Status != "Completed");

        var workloads = members.Select(m => new MemberWorkloadDto(
            m.User.Id,
            m.User.Username,
            m.User.FullName,
            m.User.Department,
            m.User.AssignedTasks.Count(t => t.WorkspaceId == workspaceId && t.Status != "Completed"),
            m.User.AssignedTasks.Count(t => t.WorkspaceId == workspaceId && t.Status == "Completed"),
            m.User.AssignedTasks.Count(t => t.WorkspaceId == workspaceId && t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Status != "Completed"),
            m.User.Role
        )).ToList();

        return new TeamAnalyticsDto(members.Count, 1, totalTasks, completedTasks, pendingReviews, overdue, workloads);
    }

    public async Task<ProjectAnalyticsDto> GetProjectAnalyticsAsync(int workspaceId)
    {
        var projects = await _context.Projects
            .Include(p => p.Milestones)
            .Include(p => p.Tasks)
            .AsSplitQuery()
            .Where(p => p.WorkspaceId == workspaceId)
            .ToListAsync();

        int totalProjects = projects.Count;
        int active = projects.Count(p => p.Status == "Active");
        int completed = projects.Count(p => p.Status == "Completed");

        int totalMilestones = projects.SelectMany(p => p.Milestones).Count();
        int reachedMilestones = projects.SelectMany(p => p.Milestones).Count(m => m.IsReached);

        int allTasks = projects.SelectMany(p => p.Tasks).Count();
        int compTasks = projects.SelectMany(p => p.Tasks).Count(t => t.Status == "Completed");
        int overallPct = allTasks > 0 ? (int)Math.Round((double)compTasks / allTasks * 100) : 0;

        return new ProjectAnalyticsDto(totalProjects, active, completed, totalMilestones, reachedMilestones, overallPct);
    }

    public async Task<BusinessAnalyticsDto> GetBusinessAnalyticsAsync(int orgId, int userId = 0, bool isPlatformAdmin = false)
    {
        if (userId > 0 && !isPlatformAdmin)
        {
            bool isMember = await _context.OrganizationMembers.AnyAsync(m => m.OrganizationId == orgId && m.UserId == userId);
            if (!isMember)
            {
                return new BusinessAnalyticsDto(0, 0, 0, 0, 0, 0, 0, 0, new List<DepartmentStatsDto>());
            }
        }

        var org = await _context.Organizations
            .Include(o => o.Members)
                .ThenInclude(m => m.User)
                    .ThenInclude(u => u.AssignedTasks)
            .Include(o => o.Workspaces)
                .ThenInclude(w => w.Projects)
            .Include(o => o.Workspaces)
                .ThenInclude(w => w.Tasks)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == orgId);

        if (org == null)
        {
            return new BusinessAnalyticsDto(0, 0, 0, 0, 0, 0, 0, 0, new List<DepartmentStatsDto>());
        }

        var orgMembers = org.Members.ToList();
        var allTasks = org.Workspaces.SelectMany(w => w.Tasks).ToList();
        int totalEmployees = orgMembers.Count;
        int totalWorkspaces = org.Workspaces.Count;
        int totalProjects = org.Workspaces.SelectMany(w => w.Projects).Count();
        int totalTasks = allTasks.Count;
        int completedTasks = allTasks.Count(t => t.Status == "Completed");
        int pendingApprovals = await _context.WorkflowApprovals.CountAsync(a => a.TaskItem.Workspace.OrganizationId == orgId && a.Status == "Pending");

        var depts = orgMembers
            .GroupBy(m => !string.IsNullOrWhiteSpace(m.Department) ? m.Department : "General")
            .Select(g =>
            {
                var userIds = g.Select(m => m.UserId).ToHashSet();
                var deptTasks = allTasks.Where(t => t.AssigneeUserId.HasValue && userIds.Contains(t.AssigneeUserId.Value)).ToList();
                int deptComp = deptTasks.Count(t => t.Status == "Completed");
                int deptOver = deptTasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Status != "Completed");

                return new DepartmentStatsDto(g.Key, g.Count(), deptTasks.Count, deptComp, deptOver);
            })
            .ToList();

        return new BusinessAnalyticsDto(1, totalEmployees, depts.Count, totalWorkspaces, totalProjects, totalTasks, pendingApprovals, completedTasks, depts);
    }

    public async Task<AdminPlatformAnalyticsDto> GetAdminPlatformAnalyticsAsync()
    {
        int users = await _context.Users.CountAsync();
        int orgs = await _context.Organizations.CountAsync();
        int ws = await _context.Workspaces.CountAsync();
        int projects = await _context.Projects.CountAsync();
        int tasks = await _context.Tasks.CountAsync();
        int approvals = await _context.WorkflowApprovals.CountAsync();
        int logs = await _context.ActivityLogs.CountAsync();
        int activeToday = await _context.Users.CountAsync(u => u.LastLoginAt.HasValue && u.LastLoginAt.Value >= DateTime.UtcNow.AddDays(-1));

        string providerDesc = "PostgreSQL (Live Production Engine)";

        return new AdminPlatformAnalyticsDto(
            users,
            orgs,
            ws,
            projects,
            tasks,
            approvals,
            logs,
            activeToday,
            "Healthy (All Microservices Active)",
            providerDesc
        );
    }
}
