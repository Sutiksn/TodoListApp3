using Microsoft.EntityFrameworkCore;
using TodoListApp2.Data;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Models.Entities;

namespace TodoListApp2.Services;

public class TaskService : ITaskService
{
    private readonly AppDbContext _context;
    private readonly IActivityService _activityService;
    private readonly INotificationService _notificationService;

    public TaskService(
        AppDbContext context,
        IActivityService activityService,
        INotificationService notificationService)
    {
        _context = context;
        _activityService = activityService;
        _notificationService = notificationService;
    }

    public async Task<List<TaskDto>> GetTasksAsync(int userId, bool isPlatformAdmin, int? workspaceId = null, int? projectId = null, string? status = null, string? priority = null, int? assigneeUserId = null, string? search = null)
    {
        var query = _context.Tasks
            .Include(t => t.Workspace)
                .ThenInclude(w => w.Members)
            .Include(t => t.Project)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssigneeUser)
            .Include(t => t.Subtasks)
            .Include(t => t.Comments)
            .AsSplitQuery()
            .AsNoTracking()
            .AsQueryable();

        if (isPlatformAdmin)
        {
            // Platform Admin only sees their own Personal workspaces and collaborative workspaces
            query = query.Where(t => (t.Workspace.Mode == "Personal" && t.Workspace.CreatedByUserId == userId) || t.Workspace.Mode != "Personal");
        }
        else
        {
            // Regular user sees their own Personal workspaces and collaborative workspaces where they are member/creator/assignee
            query = query.Where(t => 
                (t.Workspace.Mode == "Personal" && t.Workspace.CreatedByUserId == userId) ||
                (t.Workspace.Mode != "Personal" && (
                    t.Workspace.CreatedByUserId == userId || 
                    t.Workspace.Members.Any(m => m.UserId == userId) ||
                    t.CreatedByUserId == userId ||
                    t.AssigneeUserId == userId
                ))
            );
        }

        if (workspaceId.HasValue && workspaceId.Value > 0)
            query = query.Where(t => t.WorkspaceId == workspaceId.Value);

        if (projectId.HasValue && projectId.Value > 0)
            query = query.Where(t => t.ProjectId == projectId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(priority))
            query = query.Where(t => t.Priority == priority);

        if (assigneeUserId.HasValue)
            query = query.Where(t => t.AssigneeUserId == assigneeUserId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(t => t.Title.ToLower().Contains(s) || (t.Description != null && t.Description.ToLower().Contains(s)));
        }

        var tasks = await query
            .OrderByDescending(t => t.Priority == "Urgent")
            .ThenByDescending(t => t.Priority == "High")
            .ThenBy(t => t.DueDate.HasValue ? t.DueDate.Value : DateTime.MaxValue)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync();

        return tasks.Select(t => MapToDto(t, false)).ToList();
    }

    public async Task<TaskDto?> GetTaskByIdAsync(int taskId, int userId, bool isPlatformAdmin = false)
    {
        var task = await _context.Tasks
            .Include(t => t.Workspace)
                .ThenInclude(w => w.Members)
            .Include(t => t.Project)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssigneeUser)
            .Include(t => t.Subtasks.OrderBy(s => s.OrderIndex))
            .Include(t => t.Comments.OrderBy(c => c.CreatedAt))
                .ThenInclude(c => c.User)
            .Include(t => t.Dependencies)
                .ThenInclude(d => d.DependsOnTask)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) return null;

        // Personal mode workspace task is strictly private to its creator
        if (task.Workspace.Mode == "Personal" && task.Workspace.CreatedByUserId != userId)
        {
            return null;
        }

        if (task.Workspace.Mode != "Personal" && !isPlatformAdmin)
        {
            bool hasAccess = task.Workspace.CreatedByUserId == userId ||
                             task.Workspace.Members.Any(m => m.UserId == userId) ||
                             task.CreatedByUserId == userId ||
                             task.AssigneeUserId == userId;
            if (!hasAccess) return null;
        }

        return MapToDto(task, includeDetails: true);
    }

    public async Task<TaskDto> CreateTaskAsync(int userId, string username, CreateTaskRequest request, string mode = "Personal")
    {
        var workspace = await _context.Workspaces.FindAsync(request.WorkspaceId);
        if (workspace == null)
            throw new ArgumentException("Workspace not found.");

        if (workspace.Mode == "Personal" && workspace.CreatedByUserId != userId)
            throw new ArgumentException("Cannot create tasks in another user's personal workspace.");

        if (workspace.Mode != "Personal")
        {
            var user = await _context.Users.FindAsync(userId);
            bool isPrivileged = user?.Role == "PlatformAdmin" || user?.Role == "OrgAdmin" || user?.Role == "Manager" || workspace.CreatedByUserId == userId;
            if (!isPrivileged)
            {
                throw new UnauthorizedAccessException("Only Managers and Administrators are permitted to create/delegate tasks in collaborative workspaces.");
            }
        }

        if (request.AssigneeUserId.HasValue)
        {
            var assignee = await _context.Users.FindAsync(request.AssigneeUserId.Value);
            if (assignee?.Role == "PlatformAdmin")
            {
                throw new ArgumentException("Platform Administrator cannot be assigned to tasks.");
            }
        }

        var task = new TaskItem
        {
            WorkspaceId = request.WorkspaceId,
            ProjectId = request.ProjectId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Status = !string.IsNullOrWhiteSpace(request.Status) ? request.Status.Trim() : "ToDo",
            Priority = !string.IsNullOrWhiteSpace(request.Priority) ? request.Priority.Trim() : "Medium",
            Category = !string.IsNullOrWhiteSpace(request.Category) ? request.Category.Trim() : "General",
            StartDate = request.StartDate,
            DueDate = request.DueDate,
            EstimatedHours = request.EstimatedHours,
            CreatedByUserId = userId,
            AssigneeUserId = request.AssigneeUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (task.Status == "Completed")
            task.CompletedAt = DateTime.UtcNow;

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        if (request.InitialSubtasks != null && request.InitialSubtasks.Count > 0)
        {
            int order = 1;
            foreach (var stTitle in request.InitialSubtasks)
            {
                if (!string.IsNullOrWhiteSpace(stTitle))
                {
                    _context.Subtasks.Add(new Subtask
                    {
                        TaskItemId = task.Id,
                        Title = stTitle.Trim(),
                        OrderIndex = order++,
                        IsCompleted = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        // Notification if assigned to someone else
        if (request.AssigneeUserId.HasValue && request.AssigneeUserId.Value != userId)
        {
            await _notificationService.CreateNotificationAsync(
                request.AssigneeUserId.Value,
                "New Task Assigned",
                $"You were assigned task '{task.Title}' by {username}.",
                $"/tasks/{task.Id}",
                "TaskAssigned"
            );
        }

        await _activityService.LogAsync(
            workspace.OrganizationId,
            task.WorkspaceId,
            task.ProjectId,
            task.Id,
            userId,
            username,
            "TaskCreated",
            $"Created task '{task.Title}' [Status: {task.Status}, Priority: {task.Priority}]",
            workspace.Mode
        );

        return (await GetTaskByIdAsync(task.Id, userId, true))!;
    }

    public async Task<TaskDto?> UpdateTaskAsync(int userId, string username, int taskId, UpdateTaskRequest request, bool isPlatformAdmin = false)
    {
        var task = await _context.Tasks.Include(t => t.Workspace).ThenInclude(w => w.Members).FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null) return null;

        // Check permission
        if (task.Workspace.Mode == "Personal" && task.Workspace.CreatedByUserId != userId)
            return null;

        if (task.Workspace.Mode != "Personal")
        {
            bool hasAccess = isPlatformAdmin ||
                             task.Workspace.CreatedByUserId == userId ||
                             task.Workspace.Members.Any(m => m.UserId == userId) ||
                             task.CreatedByUserId == userId ||
                             task.AssigneeUserId == userId;
            if (!hasAccess) return null;
        }

        if (request.AssigneeUserId.HasValue)
        {
            var assignee = await _context.Users.FindAsync(request.AssigneeUserId.Value);
            if (assignee?.Role == "PlatformAdmin")
            {
                throw new ArgumentException("Platform Administrator cannot be assigned to tasks.");
            }
        }

        int? prevAssignee = task.AssigneeUserId;

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.ProjectId = request.ProjectId;
        task.Priority = request.Priority;
        task.Category = request.Category;
        task.StartDate = request.StartDate;
        task.DueDate = request.DueDate;
        task.EstimatedHours = request.EstimatedHours;
        task.ActualHours = request.ActualHours;
        task.AssigneeUserId = request.AssigneeUserId;
        task.UpdatedAt = DateTime.UtcNow;

        if (task.Status != request.Status)
        {
            task.Status = request.Status;
            task.CompletedAt = task.Status == "Completed" ? DateTime.UtcNow : null;
        }

        await _context.SaveChangesAsync();

        if (request.AssigneeUserId.HasValue && request.AssigneeUserId.Value != prevAssignee && request.AssigneeUserId.Value != userId)
        {
            await _notificationService.CreateNotificationAsync(
                request.AssigneeUserId.Value,
                "Task Reassigned",
                $"You were assigned task '{task.Title}' by {username}.",
                $"/tasks/{task.Id}",
                "TaskAssigned"
            );
        }

        await _activityService.LogAsync(
            task.Workspace.OrganizationId,
            task.WorkspaceId,
            task.ProjectId,
            task.Id,
            userId,
            username,
            "TaskUpdated",
            $"Updated task '{task.Title}'",
            task.Workspace.Mode
        );

        return await GetTaskByIdAsync(taskId, userId, isPlatformAdmin);
    }

    public async Task<TaskDto?> UpdateTaskStatusAsync(int userId, string username, int taskId, string newStatus, string mode = "Personal", bool isPlatformAdmin = false)
    {
        var task = await _context.Tasks.Include(t => t.Workspace).ThenInclude(w => w.Members).FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null) return null;

        // Check permission
        if (task.Workspace.Mode == "Personal" && task.Workspace.CreatedByUserId != userId)
            return null;

        if (task.Workspace.Mode != "Personal")
        {
            bool hasAccess = isPlatformAdmin ||
                             task.Workspace.CreatedByUserId == userId ||
                             task.Workspace.Members.Any(m => m.UserId == userId) ||
                             task.CreatedByUserId == userId ||
                             task.AssigneeUserId == userId;
            if (!hasAccess) return null;
        }

        string oldStatus = task.Status;
        task.Status = newStatus;
        task.UpdatedAt = DateTime.UtcNow;

        if (newStatus == "Completed")
            task.CompletedAt = DateTime.UtcNow;
        else
            task.CompletedAt = null;

        await _context.SaveChangesAsync();

        // If moved to Review stage, create a pending Workflow Approval if in Team/Project/Business mode
        if (newStatus == "Review" && (task.Workspace.Mode == "Team" || task.Workspace.Mode == "Project" || task.Workspace.Mode == "Business"))
        {
            var existingPending = await _context.WorkflowApprovals.AnyAsync(a => a.TaskItemId == taskId && a.Status == "Pending");
            if (!existingPending)
            {
                _context.WorkflowApprovals.Add(new WorkflowApproval
                {
                    TaskItemId = taskId,
                    RequesterUserId = userId,
                    Status = "Pending",
                    Comments = $"Task submitted for review by {username}",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
        }

        await _activityService.LogAsync(
            task.Workspace.OrganizationId,
            task.WorkspaceId,
            task.ProjectId,
            task.Id,
            userId,
            username,
            "StatusChanged",
            $"Advanced '{task.Title}' from {oldStatus} → {newStatus}",
            task.Workspace.Mode
        );

        return await GetTaskByIdAsync(taskId, userId, isPlatformAdmin);
    }

    public async Task<bool> DeleteTaskAsync(int userId, string username, int taskId, bool isPlatformAdmin = false)
    {
        var task = await _context.Tasks.Include(t => t.Workspace).ThenInclude(w => w.Members).FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null) return false;

        // Check permission
        if (task.Workspace.Mode == "Personal" && task.Workspace.CreatedByUserId != userId)
            return false;

        if (task.Workspace.Mode != "Personal")
        {
            var user = await _context.Users.FindAsync(userId);
            bool canDelete = isPlatformAdmin ||
                             user?.Role == "PlatformAdmin" ||
                             user?.Role == "OrgAdmin" ||
                             user?.Role == "Manager" ||
                             task.Workspace.CreatedByUserId == userId ||
                             task.CreatedByUserId == userId;
            if (!canDelete)
                return false;
        }

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();

        await _activityService.LogAsync(
            task.Workspace.OrganizationId,
            task.WorkspaceId,
            task.ProjectId,
            taskId,
            userId,
            username,
            "TaskDeleted",
            $"Deleted task '{task.Title}'",
            task.Workspace.Mode
        );

        return true;
    }

    public async Task<SubtaskDto?> AddSubtaskAsync(int taskId, int userId, CreateSubtaskRequest request, bool isPlatformAdmin = false)
    {
        var task = await _context.Tasks.Include(t => t.Workspace).ThenInclude(w => w.Members).FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null) return null;

        if (task.Workspace.Mode == "Personal" && task.Workspace.CreatedByUserId != userId)
            return null;

        if (task.Workspace.Mode != "Personal")
        {
            bool hasAccess = isPlatformAdmin ||
                             task.Workspace.CreatedByUserId == userId ||
                             task.Workspace.Members.Any(m => m.UserId == userId) ||
                             task.CreatedByUserId == userId ||
                             task.AssigneeUserId == userId;
            if (!hasAccess) return null;
        }

        int nextOrder = (await _context.Subtasks.Where(s => s.TaskItemId == taskId).CountAsync()) + 1;

        var subtask = new Subtask
        {
            TaskItemId = taskId,
            Title = request.Title.Trim(),
            OrderIndex = nextOrder,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Subtasks.Add(subtask);
        await _context.SaveChangesAsync();

        return new SubtaskDto(subtask.Id, subtask.TaskItemId, subtask.Title, subtask.IsCompleted, subtask.OrderIndex, subtask.CompletedAt);
    }

    public async Task<SubtaskDto?> ToggleSubtaskAsync(int subtaskId, int userId, bool isPlatformAdmin = false)
    {
        var subtask = await _context.Subtasks
            .Include(s => s.TaskItem)
                .ThenInclude(t => t.Workspace)
                    .ThenInclude(w => w.Members)
            .FirstOrDefaultAsync(s => s.Id == subtaskId);
        if (subtask == null) return null;

        if (subtask.TaskItem.Workspace.Mode == "Personal" && subtask.TaskItem.Workspace.CreatedByUserId != userId)
            return null;

        if (subtask.TaskItem.Workspace.Mode != "Personal")
        {
            bool hasAccess = isPlatformAdmin ||
                             subtask.TaskItem.Workspace.CreatedByUserId == userId ||
                             subtask.TaskItem.Workspace.Members.Any(m => m.UserId == userId) ||
                             subtask.TaskItem.CreatedByUserId == userId ||
                             subtask.TaskItem.AssigneeUserId == userId;
            if (!hasAccess) return null;
        }

        subtask.IsCompleted = !subtask.IsCompleted;
        subtask.CompletedAt = subtask.IsCompleted ? DateTime.UtcNow : null;
        await _context.SaveChangesAsync();

        return new SubtaskDto(subtask.Id, subtask.TaskItemId, subtask.Title, subtask.IsCompleted, subtask.OrderIndex, subtask.CompletedAt);
    }

    public async Task<bool> DeleteSubtaskAsync(int subtaskId, int userId, bool isPlatformAdmin = false)
    {
        var subtask = await _context.Subtasks
            .Include(s => s.TaskItem)
                .ThenInclude(t => t.Workspace)
                    .ThenInclude(w => w.Members)
            .FirstOrDefaultAsync(s => s.Id == subtaskId);
        if (subtask == null) return false;

        if (subtask.TaskItem.Workspace.Mode == "Personal" && subtask.TaskItem.Workspace.CreatedByUserId != userId)
            return false;

        if (subtask.TaskItem.Workspace.Mode != "Personal")
        {
            var user = await _context.Users.FindAsync(userId);
            bool canDelete = isPlatformAdmin ||
                             user?.Role == "PlatformAdmin" ||
                             user?.Role == "OrgAdmin" ||
                             user?.Role == "Manager" ||
                             subtask.TaskItem.Workspace.CreatedByUserId == userId ||
                             subtask.TaskItem.CreatedByUserId == userId;
            if (!canDelete)
                return false;
        }

        _context.Subtasks.Remove(subtask);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<CommentDto?> AddCommentAsync(int userId, int taskId, CreateCommentRequest request, bool isPlatformAdmin = false)
    {
        var task = await _context.Tasks
            .Include(t => t.Workspace)
                .ThenInclude(w => w.Members)
            .Include(t => t.AssigneeUser)
            .FirstOrDefaultAsync(t => t.Id == taskId);
        var user = await _context.Users.FindAsync(userId);
        if (task == null || user == null) return null;

        if (task.Workspace.Mode == "Personal" && task.Workspace.CreatedByUserId != userId)
            return null;

        if (task.Workspace.Mode != "Personal")
        {
            bool hasAccess = isPlatformAdmin ||
                             task.Workspace.CreatedByUserId == userId ||
                             task.Workspace.Members.Any(m => m.UserId == userId) ||
                             task.CreatedByUserId == userId ||
                             task.AssigneeUserId == userId;
            if (!hasAccess) return null;
        }

        var comment = new TaskComment
        {
            TaskItemId = taskId,
            UserId = userId,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskComments.Add(comment);
        await _context.SaveChangesAsync();

        if (task.AssigneeUserId.HasValue && task.AssigneeUserId.Value != userId)
        {
            await _notificationService.CreateNotificationAsync(
                task.AssigneeUserId.Value,
                "New Task Comment",
                $"{user.FullName} commented on task '{task.Title}'.",
                $"/tasks/{task.Id}",
                "CommentAdded"
            );
        }

        return new CommentDto(comment.Id, comment.TaskItemId, user.Id, user.Username, user.FullName, comment.Content, comment.CreatedAt);
    }

    public async Task<TaskDependencyDto?> AddDependencyAsync(int taskId, int userId, AddDependencyRequest request, bool isPlatformAdmin = false)
    {
        var task = await _context.Tasks.Include(t => t.Workspace).ThenInclude(w => w.Members).FirstOrDefaultAsync(t => t.Id == taskId);
        var dependsOn = await _context.Tasks.FindAsync(request.DependsOnTaskId);
        if (task == null || dependsOn == null || taskId == request.DependsOnTaskId)
            return null;

        if (task.Workspace.Mode == "Personal" && task.Workspace.CreatedByUserId != userId)
            return null;

        if (task.Workspace.Mode != "Personal")
        {
            bool hasAccess = isPlatformAdmin ||
                             task.Workspace.CreatedByUserId == userId ||
                             task.Workspace.Members.Any(m => m.UserId == userId) ||
                             task.CreatedByUserId == userId ||
                             task.AssigneeUserId == userId;
            if (!hasAccess) return null;
        }

        var dep = new TaskDependency
        {
            TaskItemId = taskId,
            DependsOnTaskId = request.DependsOnTaskId,
            DependencyType = request.DependencyType
        };

        _context.TaskDependencies.Add(dep);
        await _context.SaveChangesAsync();

        return new TaskDependencyDto(dep.Id, dep.TaskItemId, dep.DependsOnTaskId, dependsOn.Title, dependsOn.Status, dep.DependencyType);
    }

    public async Task<bool> RemoveDependencyAsync(int dependencyId, int userId, bool isPlatformAdmin = false)
    {
        var dep = await _context.TaskDependencies
            .Include(d => d.TaskItem)
                .ThenInclude(t => t.Workspace)
                    .ThenInclude(w => w.Members)
            .FirstOrDefaultAsync(d => d.Id == dependencyId);
        if (dep == null) return false;

        if (dep.TaskItem.Workspace.Mode == "Personal" && dep.TaskItem.Workspace.CreatedByUserId != userId)
            return false;

        if (dep.TaskItem.Workspace.Mode != "Personal")
        {
            var user = await _context.Users.FindAsync(userId);
            bool canDelete = isPlatformAdmin ||
                             user?.Role == "PlatformAdmin" ||
                             user?.Role == "OrgAdmin" ||
                             user?.Role == "Manager" ||
                             dep.TaskItem.Workspace.CreatedByUserId == userId ||
                             dep.TaskItem.CreatedByUserId == userId;
            if (!canDelete)
                return false;
        }

        _context.TaskDependencies.Remove(dep);
        await _context.SaveChangesAsync();
        return true;
    }

    private static TaskDto MapToDto(TaskItem t, bool includeDetails = false)
    {
        int totalSub = t.Subtasks?.Count ?? 0;
        int compSub = t.Subtasks?.Count(s => s.IsCompleted) ?? 0;
        int subPct = totalSub > 0 ? (int)Math.Round((double)compSub / totalSub * 100) : 0;
        bool isOverdue = t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Status != "Completed";

        List<SubtaskDto>? subList = null;
        List<CommentDto>? comList = null;
        List<TaskDependencyDto>? depList = null;

        if (includeDetails)
        {
            subList = t.Subtasks?.Select(s => new SubtaskDto(s.Id, s.TaskItemId, s.Title, s.IsCompleted, s.OrderIndex, s.CompletedAt)).ToList();
            comList = t.Comments?.Select(c => new CommentDto(c.Id, c.TaskItemId, c.UserId, c.User?.Username ?? "user", c.User?.FullName ?? "User", c.Content, c.CreatedAt)).ToList();
            depList = t.Dependencies?.Select(d => new TaskDependencyDto(d.Id, d.TaskItemId, d.DependsOnTaskId, d.DependsOnTask?.Title ?? "Task", d.DependsOnTask?.Status ?? "ToDo", d.DependencyType)).ToList();
        }

        return new TaskDto(
            t.Id,
            t.WorkspaceId,
            t.Workspace?.Name ?? "Workspace",
            t.ProjectId,
            t.Project?.Name,
            t.Title,
            t.Description,
            t.Status,
            t.Priority,
            t.Category,
            t.StartDate,
            t.DueDate,
            t.EstimatedHours,
            t.ActualHours,
            t.CreatedByUserId,
            t.CreatedByUser?.Username ?? "user",
            t.AssigneeUserId,
            t.AssigneeUser?.Username,
            t.AssigneeUser?.FullName,
            t.IsCompleted,
            isOverdue,
            totalSub,
            compSub,
            subPct,
            t.Comments?.Count ?? 0,
            t.CompletedAt,
            t.CreatedAt,
            t.UpdatedAt,
            subList,
            comList,
            depList
        );
    }
}
