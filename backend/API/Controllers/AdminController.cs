using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoListApp2.Data;
using TodoListApp2.Models.Entities;
using TodoListApp2.Services;

namespace TodoListApp2.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "PlatformAdmin,OrgAdmin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IActivityService _activityService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IOrganizationService _orgService;

    public AdminController(AppDbContext context, IActivityService activityService, IWorkspaceService workspaceService, IOrganizationService orgService)
    {
        _context = context;
        _activityService = activityService;
        _workspaceService = workspaceService;
        _orgService = orgService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .Include(u => u.AssignedTasks)
                .ThenInclude(t => t.Workspace)
            .OrderByDescending(u => u.Role == "PlatformAdmin")
            .ThenBy(u => u.Username)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                u.FullName,
                u.Role,
                u.Department,
                u.IsActive,
                u.CreatedAt,
                u.LastLoginAt,
                ActiveTasks = u.AssignedTasks.Count(t => t.Workspace.Mode != "Personal" && t.Status != "Completed"),
                CompletedTasks = u.AssignedTasks.Count(t => t.Workspace.Mode != "Personal" && t.Status == "Completed")
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPatch("users/{id}/toggle-status")]
    public async Task<IActionResult> ToggleUserStatus(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound(new { message = "User not found." });

        if (user.Role == "PlatformAdmin")
            return BadRequest(new { message = "Cannot deactivate platform administrator." });

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync();

        return Ok(new { message = $"User {(user.IsActive ? "activated" : "deactivated")} successfully.", isActive = user.IsActive });
    }

    [HttpPatch("users/{id}/role")]
    public async Task<IActionResult> ChangeUserRole(int id, [FromBody] ChangeRoleRequest request)
    {
        var user = await _context.Users
            .Include(u => u.OrganizationMemberships)
            .Include(u => u.WorkspaceMemberships)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound(new { message = "User not found." });

        string oldRole = user.Role;
        string newRole = request.Role;
        if (newRole == "OrgOwner") newRole = "OrgAdmin";
        user.Role = newRole;

        // If user is promoted to OrgAdmin:
        if (newRole == "OrgAdmin")
        {
            var userOrgMember = user.OrganizationMemberships.FirstOrDefault();
            Organization? org = null;

            if (userOrgMember != null)
            {
                org = await _context.Organizations
                    .Include(o => o.Members)
                    .Include(o => o.Workspaces)
                        .ThenInclude(w => w.Members)
                    .FirstOrDefaultAsync(o => o.Id == userOrgMember.OrganizationId);
            }
            else
            {
                org = await _context.Organizations
                    .Include(o => o.Members)
                    .Include(o => o.Workspaces)
                        .ThenInclude(w => w.Members)
                    .FirstOrDefaultAsync(o => o.CreatedByUserId == user.Id);

                if (org == null)
                {
                    var req = await _context.AccessRequests
                        .Where(r => r.UserId == user.Id && r.OrganizationId.HasValue)
                        .OrderByDescending(r => r.CreatedAt)
                        .FirstOrDefaultAsync();
                    if (req?.OrganizationId != null)
                    {
                        org = await _context.Organizations
                            .Include(o => o.Members)
                            .Include(o => o.Workspaces)
                                .ThenInclude(w => w.Members)
                            .FirstOrDefaultAsync(o => o.Id == req.OrganizationId.Value);
                    }
                }
            }

            if (org != null)
            {
                int oldOwnerId = org.CreatedByUserId;
                org.CreatedByUserId = user.Id;

                var curMember = org.Members.FirstOrDefault(m => m.UserId == user.Id);
                if (curMember == null)
                {
                    _context.OrganizationMembers.Add(new OrganizationMember
                    {
                        OrganizationId = org.Id,
                        UserId = user.Id,
                        Role = "OrgAdmin",
                        Department = user.Department ?? "Executive",
                        JoinedAt = DateTime.UtcNow,
                        Status = "Active"
                    });
                }
                else
                {
                    curMember.Role = "OrgAdmin";
                    curMember.Status = "Active";
                }

                // If previous owner/admin was someone else, adjust their OrgMember role to Manager if they were OrgAdmin
                if (oldOwnerId != 0 && oldOwnerId != user.Id)
                {
                    var prevOwner = await _context.Users.FindAsync(oldOwnerId);
                    if (prevOwner != null && prevOwner.Role == "OrgAdmin")
                    {
                        prevOwner.Role = "Manager";
                    }
                    var prevMember = org.Members.FirstOrDefault(m => m.UserId == oldOwnerId);
                    if (prevMember != null && prevMember.Role == "OrgAdmin")
                    {
                        prevMember.Role = "Manager";
                    }
                }

                // Ensure new OrgAdmin is an admin member of the collaborative workspaces
                var collabWorkspaces = org.Workspaces.Where(w => w.Mode == "Business" || w.Mode == "Team").ToList();
                foreach (var ws in collabWorkspaces)
                {
                    var wsMember = ws.Members.FirstOrDefault(m => m.UserId == user.Id);
                    if (wsMember == null)
                    {
                        _context.WorkspaceMembers.Add(new WorkspaceMember
                        {
                            WorkspaceId = ws.Id,
                            UserId = user.Id,
                            Role = "Admin",
                            JoinedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        wsMember.Role = "Admin";
                    }
                }
            }
        }
        else
        {
            // If user's role is set to Manager, Employee, or User:
            foreach (var om in user.OrganizationMemberships)
            {
                om.Role = newRole;
            }
        }

        await _context.SaveChangesAsync();
        await _activityService.LogAsync(null, null, null, null, user.Id, user.Username, "RoleChanged", $"Administrator updated role of user '{user.Username}' from '{oldRole}' to '{newRole}'", "Admin");

        return Ok(new { message = $"User role updated to '{user.Role}'.", role = user.Role });
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users
            .Include(u => u.WorkspaceMemberships)
            .Include(u => u.OrganizationMemberships)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound(new { message = "User not found." });

        if (user.Role == "PlatformAdmin")
            return BadRequest(new { message = "Platform Administrator account cannot be deleted." });

        // 1. Unassign user from all assigned tasks
        var assignedTasks = await _context.Tasks.Where(t => t.AssigneeUserId == id).ToListAsync();
        foreach (var task in assignedTasks)
        {
            task.AssigneeUserId = null;
        }

        // 2. Delete personal workspaces created by this user
        var personalWorkspaces = await _context.Workspaces
            .Where(w => w.CreatedByUserId == id && w.Mode == "Personal")
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
            .ToListAsync();
        _context.Workspaces.RemoveRange(personalWorkspaces);

        // 3. For tasks created by this user in collaborative workspaces: reassign CreatedByUserId
        var createdTasks = await _context.Tasks
            .Include(t => t.Workspace)
            .Where(t => t.CreatedByUserId == id && t.Workspace.Mode != "Personal")
            .ToListAsync();
        foreach (var ct in createdTasks)
        {
            if (ct.Workspace.CreatedByUserId != id)
            {
                ct.CreatedByUserId = ct.Workspace.CreatedByUserId;
            }
            else
            {
                var fallbackAdmin = await _context.Users.FirstOrDefaultAsync(u => u.Role == "PlatformAdmin");
                if (fallbackAdmin != null) ct.CreatedByUserId = fallbackAdmin.Id;
            }
        }

        // 4. For projects created by this user in collaborative workspaces:
        var createdProjects = await _context.Projects
            .Include(p => p.Workspace)
            .Where(p => p.CreatedByUserId == id)
            .ToListAsync();
        foreach (var cp in createdProjects)
        {
            if (cp.Workspace.CreatedByUserId != id)
            {
                cp.CreatedByUserId = cp.Workspace.CreatedByUserId;
            }
            else
            {
                var fallbackAdmin = await _context.Users.FirstOrDefaultAsync(u => u.Role == "PlatformAdmin");
                if (fallbackAdmin != null) cp.CreatedByUserId = fallbackAdmin.Id;
            }
        }

        // 5. For collaborative workspaces created by this user:
        var collaborativeWorkspaces = await _context.Workspaces
            .Where(w => w.CreatedByUserId == id && w.Mode != "Personal")
            .ToListAsync();
        foreach (var cw in collaborativeWorkspaces)
        {
            var fallbackAdmin = await _context.Users.FirstOrDefaultAsync(u => u.Role == "PlatformAdmin");
            if (fallbackAdmin != null) cw.CreatedByUserId = fallbackAdmin.Id;
        }

        // 6. For organizations created by this user:
        var createdOrgs = await _context.Organizations.Where(o => o.CreatedByUserId == id).ToListAsync();
        foreach (var co in createdOrgs)
        {
            var fallbackAdmin = await _context.Users.FirstOrDefaultAsync(u => u.Role == "PlatformAdmin");
            if (fallbackAdmin != null) co.CreatedByUserId = fallbackAdmin.Id;
        }

        // 7. Clean up workflow approvals
        var approvals = await _context.WorkflowApprovals.Where(a => a.RequesterUserId == id || a.ApproverUserId == id).ToListAsync();
        _context.WorkflowApprovals.RemoveRange(approvals);

        // 8. Clean up access requests
        var accessRequests = await _context.AccessRequests.Where(a => a.UserId == id || a.TargetManagerUserId == id || a.DecidedByUserId == id).ToListAsync();
        _context.AccessRequests.RemoveRange(accessRequests);

        // 9. Clean up task comments
        var comments = await _context.TaskComments.Where(c => c.UserId == id).ToListAsync();
        _context.TaskComments.RemoveRange(comments);

        // 10. Clean up notifications
        var notifs = await _context.Notifications.Where(n => n.UserId == id).ToListAsync();
        _context.Notifications.RemoveRange(notifs);

        // 11. Clean up memberships
        var wsMembers = await _context.WorkspaceMembers.Where(wm => wm.UserId == id).ToListAsync();
        _context.WorkspaceMembers.RemoveRange(wsMembers);

        var orgMembers = await _context.OrganizationMembers.Where(om => om.UserId == id).ToListAsync();
        _context.OrganizationMembers.RemoveRange(orgMembers);

        // 12. Remove User
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        await _activityService.LogAsync(null, null, null, null, id, user.Username, "UserDeleted", $"Admin deleted user '{user.Username}'", "Admin");

        return Ok(new { message = $"User '{user.Username}' has been removed from the platform." });
    }

    [HttpGet("organizations")]
    public async Task<IActionResult> GetOrganizations()
    {
        var orgs = await _context.Organizations
            .Include(o => o.Members)
            .Include(o => o.Workspaces)
            .Select(o => new
            {
                o.Id,
                o.Name,
                o.Slug,
                o.Description,
                o.SubscriptionPlan,
                o.MaxMembers,
                MemberCount = o.Members.Count,
                WorkspaceCount = o.Workspaces.Count,
                o.CreatedAt
            })
            .ToListAsync();

        return Ok(orgs);
    }

    [HttpDelete("organizations/{id}")]
    public async Task<IActionResult> DeleteOrganization(int id)
    {
        var org = await _context.Organizations.FindAsync(id);
        if (org == null) return NotFound(new { message = "Organization not found." });

        int userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int uid) ? uid : 0;
        bool deleted = await _orgService.DeleteOrganizationAsync(id, userId, isPlatformAdmin: true);
        if (!deleted) return BadRequest(new { message = "Failed to delete organization." });

        return Ok(new { message = $"Organization '{org.Name}' deleted successfully." });
    }

    [HttpDelete("organizations/clear-all")]
    public async Task<IActionResult> ClearAllOrganizations()
    {
        var orgs = await _context.Organizations.ToListAsync();
        int userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int uid) ? uid : 0;
        
        foreach (var org in orgs)
        {
            await _orgService.DeleteOrganizationAsync(org.Id, userId, isPlatformAdmin: true);
        }

        // Clean up any remaining role access requests
        var accessRequests = await _context.AccessRequests.ToListAsync();
        _context.AccessRequests.RemoveRange(accessRequests);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Removed all {orgs.Count} target organizations successfully." });
    }

    [HttpGet("workspaces")]
    public async Task<IActionResult> GetWorkspaces()
    {
        var workspaces = await _context.Workspaces
            .Include(w => w.Organization)
            .Include(w => w.Tasks)
            .Include(w => w.Projects)
            .Include(w => w.Members)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

        var userIds = workspaces.Select(w => w.CreatedByUserId).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.Username, u.FullName });

        var result = workspaces.Select(w => new
        {
            w.Id,
            w.Name,
            w.Description,
            w.Mode,
            w.Color,
            w.OrganizationId,
            OrganizationName = w.Organization != null ? w.Organization.Name : null,
            w.CreatedByUserId,
            CreatedByUsername = users.ContainsKey(w.CreatedByUserId) ? users[w.CreatedByUserId].Username : null,
            CreatedByFullName = users.ContainsKey(w.CreatedByUserId) ? users[w.CreatedByUserId].FullName : null,
            TaskCount = w.Tasks.Count,
            ProjectCount = w.Projects.Count,
            MemberCount = w.Members.Count,
            w.CreatedAt
        });

        return Ok(result);
    }

    [HttpDelete("workspaces/{id}")]
    public async Task<IActionResult> DeleteWorkspaceByAdmin(int id)
    {
        var ws = await _context.Workspaces.FindAsync(id);
        if (ws == null) return NotFound(new { message = "Workspace not found." });

        int userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int uid) ? uid : 0;
        bool deleted = await _workspaceService.DeleteWorkspaceAsync(id, userId, isPlatformAdmin: true);
        if (!deleted) return BadRequest(new { message = "Failed to delete workspace." });

        return Ok(new { message = $"Workspace '{ws.Name}' was deleted by admin." });
    }

    [HttpGet("activity-logs")]
    public async Task<IActionResult> GetActivityLogs([FromQuery] string? mode = null, [FromQuery] int limit = 100)
    {
        var logs = await _activityService.GetActivitiesAsync(null, null, null, mode, limit, excludePersonal: false);
        return Ok(logs);
    }

    [HttpDelete("activity-logs/clear")]
    public async Task<IActionResult> ClearActivityLogs()
    {
        var logs = await _context.ActivityLogs.ToListAsync();
        _context.ActivityLogs.RemoveRange(logs);
        await _context.SaveChangesAsync();
        return Ok(new { message = $"Cleared {logs.Count} audit and activity logs successfully." });
    }
}

public record ChangeRoleRequest(string Role);
