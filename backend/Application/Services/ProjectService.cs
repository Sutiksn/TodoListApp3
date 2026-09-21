using Microsoft.EntityFrameworkCore;
using TodoListApp2.Data;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Models.Entities;

namespace TodoListApp2.Services;

public class ProjectService : IProjectService
{
    private readonly AppDbContext _context;
    private readonly IActivityService _activityService;

    public ProjectService(AppDbContext context, IActivityService activityService)
    {
        _context = context;
        _activityService = activityService;
    }

    public async Task<List<GoalDto>> GetGoalsAsync(int workspaceId)
    {
        var goals = await _context.Goals
            .Include(g => g.Projects)
            .Where(g => g.WorkspaceId == workspaceId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        return goals.Select(g => new GoalDto(
            g.Id,
            g.WorkspaceId,
            g.Title,
            g.Description,
            g.Status,
            g.ProgressPercentage,
            g.TargetDate,
            g.Projects.Count,
            g.CreatedAt
        )).ToList();
    }

    public async Task<GoalDto> CreateGoalAsync(int userId, CreateGoalRequest request, bool isPlatformAdmin = false)
    {
        var user = await _context.Users.FindAsync(userId);
        bool isManagerOrAdmin = isPlatformAdmin || user?.Role == "PlatformAdmin" || user?.Role == "OrgAdmin" || user?.Role == "Manager";
        if (!isManagerOrAdmin)
        {
            throw new UnauthorizedAccessException("Only Managers and Administrators are permitted to create strategic goals.");
        }

        var goal = new Goal
        {
            WorkspaceId = request.WorkspaceId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            TargetDate = request.TargetDate,
            Status = request.Status,
            ProgressPercentage = 0,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Goals.Add(goal);
        await _context.SaveChangesAsync();

        return new GoalDto(goal.Id, goal.WorkspaceId, goal.Title, goal.Description, goal.Status, goal.ProgressPercentage, goal.TargetDate, 0, goal.CreatedAt);
    }

    public async Task<List<ProjectDto>> GetProjectsAsync(int workspaceId, int? goalId = null)
    {
        var query = _context.Projects
            .Include(p => p.Goal)
            .Include(p => p.Milestones)
            .Include(p => p.Tasks)
            .AsSplitQuery()
            .Where(p => p.WorkspaceId == workspaceId)
            .AsQueryable();

        if (goalId.HasValue && goalId.Value > 0)
            query = query.Where(p => p.GoalId == goalId.Value);

        var projects = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

        return projects.Select(MapToDto).ToList();
    }

    public async Task<ProjectDto?> GetProjectByIdAsync(int projectId)
    {
        var p = await _context.Projects
            .Include(p => p.Goal)
            .Include(p => p.Milestones)
            .Include(p => p.Tasks)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == projectId);

        return p != null ? MapToDto(p) : null;
    }

    public async Task<ProjectDto> CreateProjectAsync(int userId, CreateProjectRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        bool isManagerOrAdmin = user?.Role == "PlatformAdmin" || user?.Role == "OrgAdmin" || user?.Role == "Manager";
        if (!isManagerOrAdmin)
        {
            throw new UnauthorizedAccessException("Only Managers and Organization Administrators are permitted to create projects.");
        }

        var project = new Project
        {
            WorkspaceId = request.WorkspaceId,
            GoalId = request.GoalId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Color = !string.IsNullOrWhiteSpace(request.Color) ? request.Color : "#6366f1",
            Priority = request.Priority,
            Status = "Active",
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        return (await GetProjectByIdAsync(project.Id))!;
    }

    public async Task<ProjectDto?> UpdateProjectAsync(int projectId, CreateProjectRequest request, int userId = 0, bool isPlatformAdmin = false)
    {
        var project = await _context.Projects.FindAsync(projectId);
        if (project == null) return null;

        var user = await _context.Users.FindAsync(userId);
        bool isManagerOrAdmin = isPlatformAdmin || user?.Role == "PlatformAdmin" || user?.Role == "OrgAdmin" || user?.Role == "Manager" || project.CreatedByUserId == userId;
        if (!isManagerOrAdmin) return null;

        project.Name = request.Name.Trim();
        project.Description = request.Description?.Trim();
        project.Color = request.Color;
        project.Priority = request.Priority;
        project.GoalId = request.GoalId;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;

        await _context.SaveChangesAsync();
        return await GetProjectByIdAsync(projectId);
    }

    public async Task<bool> DeleteProjectAsync(int projectId, int userId, bool isPlatformAdmin = false)
    {
        var project = await _context.Projects
            .Include(p => p.Workspace)
            .Include(p => p.Milestones)
            .Include(p => p.Tasks)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) return false;

        var user = await _context.Users.FindAsync(userId);
        bool isManagerOrAdmin = isPlatformAdmin || user?.Role == "PlatformAdmin" || user?.Role == "OrgAdmin" || user?.Role == "Manager" || project.CreatedByUserId == userId;
        if (!isManagerOrAdmin)
        {
            return false;
        }

        // 1. Remove all tasks belonging to the project
        var projectTasks = await _context.Tasks
            .Where(t => t.ProjectId == projectId)
            .ToListAsync();
        if (projectTasks.Any())
        {
            _context.Tasks.RemoveRange(projectTasks);
        }

        // 2. Remove all milestones belonging to the project
        var projectMilestones = await _context.Milestones
            .Where(m => m.ProjectId == projectId)
            .ToListAsync();
        if (projectMilestones.Any())
        {
            _context.Milestones.RemoveRange(projectMilestones);
        }

        // 3. Remove the project itself
        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        // 4. Log the activity
        await _activityService.LogAsync(
            project.Workspace?.OrganizationId,
            project.WorkspaceId,
            projectId,
            null,
            userId,
            user?.Username ?? "system",
            "ProjectDeleted",
            $"Deleted project '{project.Name}' and all associated tasks and milestones",
            project.Workspace?.Mode ?? "Project"
        );

        return true;
    }

    public async Task<List<MilestoneDto>> GetMilestonesAsync(int projectId)
    {
        return await _context.Milestones
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.DueDate)
            .Select(m => new MilestoneDto(m.Id, m.ProjectId, m.Title, m.Description, m.DueDate, m.IsReached, m.ReachedAt))
            .ToListAsync();
    }

    public async Task<MilestoneDto> CreateMilestoneAsync(CreateMilestoneRequest request, int userId = 0, bool isPlatformAdmin = false)
    {
        var milestone = new Milestone
        {
            ProjectId = request.ProjectId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            DueDate = request.DueDate,
            IsReached = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Milestones.Add(milestone);
        await _context.SaveChangesAsync();

        return new MilestoneDto(milestone.Id, milestone.ProjectId, milestone.Title, milestone.Description, milestone.DueDate, milestone.IsReached, milestone.ReachedAt);
    }

    public async Task<MilestoneDto?> ToggleMilestoneAsync(int milestoneId, int userId = 0, bool isPlatformAdmin = false)
    {
        var m = await _context.Milestones.FindAsync(milestoneId);
        if (m == null) return null;

        m.IsReached = !m.IsReached;
        m.ReachedAt = m.IsReached ? DateTime.UtcNow : null;
        await _context.SaveChangesAsync();

        return new MilestoneDto(m.Id, m.ProjectId, m.Title, m.Description, m.DueDate, m.IsReached, m.ReachedAt);
    }

    private static ProjectDto MapToDto(Project p)
    {
        int total = p.Tasks?.Count ?? 0;
        int completed = p.Tasks?.Count(t => t.Status == "Completed") ?? 0;
        int progress = total > 0 ? (int)Math.Round((double)completed / total * 100) : 0;

        return new ProjectDto(
            p.Id,
            p.WorkspaceId,
            p.GoalId,
            p.Goal?.Title,
            p.Name,
            p.Description,
            p.Color,
            p.Status,
            p.Priority,
            p.StartDate,
            p.EndDate,
            progress,
            total,
            completed,
            total - completed,
            p.Milestones?.Count ?? 0,
            p.CreatedAt,
            p.CreatedByUserId
        );
    }
}
