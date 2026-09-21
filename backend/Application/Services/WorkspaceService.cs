using Microsoft.EntityFrameworkCore;
using TodoListApp2.Data;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Models.Entities;

namespace TodoListApp2.Services;

public class WorkspaceService : IWorkspaceService
{
    private readonly AppDbContext _context;
    private readonly IActivityService _activityService;

    public WorkspaceService(AppDbContext context, IActivityService activityService)
    {
        _context = context;
        _activityService = activityService;
    }

    public async Task<List<WorkspaceDto>> GetWorkspacesForUserAsync(int userId, string? mode = null)
    {
        var user = await _context.Users.FindAsync(userId);
        bool isPlatformAdmin = user?.Role == "PlatformAdmin";

        var query = _context.Workspaces
            .Include(w => w.Members)
            .Include(w => w.Tasks)
            .Include(w => w.Projects)
            .AsSplitQuery()
            .AsQueryable();

        if (isPlatformAdmin)
        {
            // Platform Admin only manages the platform & organizations, and is not part of internal teams/projects.
            // Only show Admin's own Personal workspace and dedicated Organization Governance (Business) workspaces.
            query = query.Where(w => (w.Mode == "Personal" && w.CreatedByUserId == userId) || w.Mode == "Business");
        }
        else
        {
            // Regular user only sees their own Personal workspaces and collaborative workspaces where they are member/creator
            query = query.Where(w => 
                (w.Mode == "Personal" && w.CreatedByUserId == userId) || 
                (w.Mode != "Personal" && (w.CreatedByUserId == userId || w.Members.Any(m => m.UserId == userId)))
            );
        }

        if (!string.IsNullOrWhiteSpace(mode))
        {
            query = query.Where(w => w.Mode.ToLower() == mode.Trim().ToLower());
        }

        var workspaces = await query.OrderByDescending(w => w.CreatedAt).ToListAsync();

        return workspaces.Select(w => new WorkspaceDto(
            w.Id,
            w.OrganizationId,
            w.Name,
            w.Description,
            w.Mode,
            w.Color,
            w.CreatedByUserId,
            w.Tasks.Count,
            w.Tasks.Count(t => t.Status == "Completed"),
            w.Projects.Count,
            w.Members.Count,
            w.CreatedAt
        )).ToList();
    }

    public async Task<WorkspaceDto?> GetWorkspaceByIdAsync(int workspaceId, int userId, bool isPlatformAdmin = false)
    {
        var w = await _context.Workspaces
            .Include(w => w.Members)
            .Include(w => w.Tasks)
            .Include(w => w.Projects)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == workspaceId);

        if (w == null) return null;

        // Personal mode workspace is strictly confidential to its creator
        if (w.Mode == "Personal" && w.CreatedByUserId != userId)
        {
            return null;
        }

        if (w.Mode != "Personal" && !isPlatformAdmin && w.CreatedByUserId != userId && !w.Members.Any(m => m.UserId == userId))
        {
            return null;
        }

        return new WorkspaceDto(
            w.Id,
            w.OrganizationId,
            w.Name,
            w.Description,
            w.Mode,
            w.Color,
            w.CreatedByUserId,
            w.Tasks.Count,
            w.Tasks.Count(t => t.Status == "Completed"),
            w.Projects.Count,
            w.Members.Count,
            w.CreatedAt
        );
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(int userId, CreateWorkspaceRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) throw new UnauthorizedAccessException("User not found.");

        string mode = !string.IsNullOrWhiteSpace(request.Mode) ? request.Mode.Trim() : "Personal";

        // User & Employee: only Personal mode
        if ((user.Role == "User" || user.Role == "Employee" || user.Role == "Member" || user.Role == "Guest") && !mode.Equals("Personal", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Users and Employees are only permitted to create Personal workspaces.");
        }

        // Manager: can create Personal, Team, Project (not Business)
        if (user.Role == "Manager" && mode.Equals("Business", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Managers cannot create Organization Governance (Business) workspaces.");
        }

        // Check overall permissions for collaborative workspaces
        if (!mode.Equals("Personal", StringComparison.OrdinalIgnoreCase))
        {
            bool isManagerOrAdmin = user.Role == "PlatformAdmin" || user.Role == "OrgAdmin" || user.Role == "Manager";
            if (!isManagerOrAdmin)
            {
                throw new UnauthorizedAccessException("Only Managers and Administrators are permitted to create collaborative workspaces.");
            }
        }

        // Link collaborative workspace to user's Organization if not explicitly passed
        int? orgId = request.OrganizationId;
        if (!orgId.HasValue && !mode.Equals("Personal", StringComparison.OrdinalIgnoreCase))
        {
            var userOrgMember = await _context.OrganizationMembers.FirstOrDefaultAsync(om => om.UserId == userId);
            orgId = userOrgMember?.OrganizationId;
        }

        // Enforce consistent mode branding colors (do not allow color tampering)
        string modeColor = mode.Trim().ToLowerInvariant() switch
        {
            "team" => "#0284c7",
            "project" => "#7c3aed",
            "business" => "#059669",
            _ => "#4f46e5"
        };

        var workspace = new Workspace
        {
            OrganizationId = mode.Equals("Personal", StringComparison.OrdinalIgnoreCase) ? null : orgId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Mode = mode,
            Color = modeColor,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        _context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = userId,
            Role = "Admin",
            JoinedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        return (await GetWorkspaceByIdAsync(workspace.Id, userId, true))!;
    }

    public async Task<WorkspaceDto?> UpdateWorkspaceAsync(int workspaceId, int userId, CreateWorkspaceRequest request, bool isPlatformAdmin = false)
    {
        var w = await _context.Workspaces.FindAsync(workspaceId);
        if (w == null) return null;

        if (w.Mode == "Personal" && w.CreatedByUserId != userId)
        {
            return null;
        }

        var user = await _context.Users.FindAsync(userId);
        bool canManage = isPlatformAdmin || user?.Role == "PlatformAdmin" || user?.Role == "OrgAdmin" || w.CreatedByUserId == userId;
        if (w.Mode != "Personal" && !canManage)
        {
            return null;
        }

        string modeColor = request.Mode?.Trim().ToLowerInvariant() switch
        {
            "team" => "#0284c7",
            "project" => "#7c3aed",
            "business" => "#059669",
            _ => "#4f46e5"
        };

        w.Name = request.Name.Trim();
        w.Description = request.Description?.Trim();
        w.Mode = !string.IsNullOrWhiteSpace(request.Mode) ? request.Mode : w.Mode;
        w.Color = modeColor;
        w.OrganizationId = request.OrganizationId;

        await _context.SaveChangesAsync();
        return await GetWorkspaceByIdAsync(workspaceId, userId, isPlatformAdmin);
    }

    public async Task<bool> DeleteWorkspaceAsync(int workspaceId, int userId, bool isPlatformAdmin = false)
    {
        var w = await _context.Workspaces
            .Include(w => w.Tasks)
                .ThenInclude(t => t.Subtasks)
            .Include(w => w.Tasks)
                .ThenInclude(t => t.Comments)
            .Include(w => w.Tasks)
                .ThenInclude(t => t.Approvals)
            .Include(w => w.Projects)
                .ThenInclude(p => p.Milestones)
            .Include(w => w.Goals)
            .Include(w => w.Members)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == workspaceId);

        if (w == null) return false;

        var user = await _context.Users.FindAsync(userId);
        bool isAdmin = isPlatformAdmin || user?.Role == "PlatformAdmin" || user?.Role == "OrgAdmin";
        bool isCreator = w.CreatedByUserId == userId;

        // Admins (PlatformAdmin, OrgAdmin) or the workspace creator can delete workspaces
        if (!isAdmin && !isCreator)
        {
            return false;
        }

        // Clean up any dependent child entities safely
        foreach (var task in w.Tasks)
        {
            _context.TaskComments.RemoveRange(task.Comments);
            _context.WorkflowApprovals.RemoveRange(task.Approvals);
            _context.Subtasks.RemoveRange(task.Subtasks);
        }
        _context.Tasks.RemoveRange(w.Tasks);

        foreach (var proj in w.Projects)
        {
            _context.Milestones.RemoveRange(proj.Milestones);
        }
        _context.Projects.RemoveRange(w.Projects);

        _context.Goals.RemoveRange(w.Goals);
        _context.WorkspaceMembers.RemoveRange(w.Members);

        _context.Workspaces.Remove(w);
        await _context.SaveChangesAsync();

        await _activityService.LogAsync(
            w.OrganizationId,
            w.Id,
            null,
            null,
            userId,
            user?.Username ?? "Admin",
            "WorkspaceDeleted",
            $"Deleted workspace '{w.Name}' ({w.Mode} mode)",
            w.Mode
        );

        return true;
    }

    public async Task<List<WorkspaceMemberDto>> GetWorkspaceMembersAsync(int workspaceId)
    {
        return await _context.WorkspaceMembers
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.WorkspaceId == workspaceId && m.User.Role != "PlatformAdmin")
            .Select(m => new WorkspaceMemberDto(
                m.Id,
                m.WorkspaceId,
                m.UserId,
                m.User.Username,
                m.User.FullName,
                m.User.Email,
                m.Role,
                m.JoinedAt,
                m.User.Role
            ))
            .ToListAsync();
    }

    public async Task<WorkspaceMemberDto?> AddWorkspaceMemberAsync(int workspaceId, AddWorkspaceMemberRequest request)
    {
        var ws = await _context.Workspaces.FindAsync(workspaceId);
        if (ws == null) return null;

        var input = request.UsernameOrEmail.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == input || u.Email.ToLower() == input);
        if (user == null) return null;

        // Platform Admin cannot be added as a workspace member
        if (user.Role == "PlatformAdmin")
        {
            throw new InvalidOperationException("Platform Administrator cannot be added as a workspace member.");
        }

        // Strict Organization Isolation:
        // If workspace belongs to an organization, user must belong to that organization!
        if (ws.OrganizationId.HasValue)
        {
            bool isMemberOfOrg = await _context.OrganizationMembers
                .AnyAsync(om => om.OrganizationId == ws.OrganizationId.Value && om.UserId == user.Id);
            if (!isMemberOfOrg)
            {
                throw new InvalidOperationException("Strict Isolation: Only employees and managers belonging to this organization can be added to this workspace.");
            }
        }

        var existing = await _context.WorkspaceMembers.FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == user.Id);
        if (existing != null)
        {
            throw new InvalidOperationException($"User '{user.FullName ?? user.Username}' is already an enrolled member of this workspace.");
        }

        if (request.ProjectId.HasValue)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId.Value && p.WorkspaceId == workspaceId);
            if (project == null)
            {
                throw new ArgumentException("The specified project was not found in this workspace.");
            }

            await _activityService.LogAsync(
                ws.OrganizationId,
                ws.Id,
                project.Id,
                null,
                user.Id,
                user.Username,
                "ProjectMemberAssigned",
                $"Assigned {user.FullName} ({user.Role}) to project '{project.Name}' as {request.Role}",
                ws.Mode
            );
        }

        var member = new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = user.Id,
            Role = request.Role,
            JoinedAt = DateTime.UtcNow
        };

        _context.WorkspaceMembers.Add(member);
        await _context.SaveChangesAsync();

        return new WorkspaceMemberDto(member.Id, member.WorkspaceId, user.Id, user.Username, user.FullName, user.Email, member.Role, member.JoinedAt, user.Role);
    }

    public async Task<bool> RemoveWorkspaceMemberAsync(int workspaceId, int targetUserIdOrMemberId, int currentUserId, bool isPlatformAdmin = false)
    {
        var ws = await _context.Workspaces.FindAsync(workspaceId);
        if (ws == null) return false;

        var user = await _context.Users.FindAsync(currentUserId);
        bool canRemove = isPlatformAdmin || user?.Role == "PlatformAdmin" || user?.Role == "OrgAdmin" || ws.CreatedByUserId == currentUserId || currentUserId == targetUserIdOrMemberId;
        if (!canRemove)
        {
            return false;
        }

        var member = await _context.WorkspaceMembers.FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && (m.Id == targetUserIdOrMemberId || m.UserId == targetUserIdOrMemberId));
        if (member == null) return false;

        // Do not allow deleting creator unless workspace is deleted
        if (member.UserId == ws.CreatedByUserId && currentUserId != ws.CreatedByUserId)
        {
            return false;
        }

        _context.WorkspaceMembers.Remove(member);
        await _context.SaveChangesAsync();
        return true;
    }
}
