using Microsoft.EntityFrameworkCore;
using TodoListApp2.Data;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Models.Entities;

namespace TodoListApp2.Services;

public class ActivityService : IActivityService
{
    private readonly AppDbContext _context;

    public ActivityService(AppDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(int? organizationId, int? workspaceId, int? projectId, int? taskId, int userId, string username, string action, string details, string mode = "Personal")
    {
        try
        {
            var log = new ActivityLog
            {
                OrganizationId = organizationId,
                WorkspaceId = workspaceId,
                ProjectId = projectId,
                TaskId = taskId,
                UserId = userId,
                Username = username,
                Action = action,
                Details = details,
                Mode = mode,
                Timestamp = DateTime.UtcNow
            };
            _context.ActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch
        {
            // Fail-safe for background logging
        }
    }

    public async Task<List<ActivityLogDto>> GetActivitiesAsync(int? workspaceId = null, int? projectId = null, int? userId = null, string? mode = null, int limit = 50, bool excludePersonal = false)
    {
        var query = _context.ActivityLogs.AsNoTracking().AsQueryable();

        if (excludePersonal)
            query = query.Where(a => a.Mode != "Personal");

        if (workspaceId.HasValue)
            query = query.Where(a => a.WorkspaceId == workspaceId.Value);

        if (projectId.HasValue)
            query = query.Where(a => a.ProjectId == projectId.Value);

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(mode))
            query = query.Where(a => a.Mode == mode);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .Select(a => new ActivityLogDto(a.Id, a.OrganizationId, a.WorkspaceId, a.ProjectId, a.TaskId, a.UserId, a.Username, a.Action, a.Details, a.Mode, a.Timestamp))
            .ToListAsync();
    }

    public async Task<int> ClearActivitiesAsync(int? workspaceId = null, string? mode = null)
    {
        var query = _context.ActivityLogs.AsQueryable();

        if (workspaceId.HasValue)
            query = query.Where(a => a.WorkspaceId == workspaceId.Value);

        if (!string.IsNullOrWhiteSpace(mode))
            query = query.Where(a => a.Mode == mode);

        var logs = await query.ToListAsync();
        _context.ActivityLogs.RemoveRange(logs);
        await _context.SaveChangesAsync();
        return logs.Count;
    }
}
