using Microsoft.EntityFrameworkCore;
using TodoListApp2.Data;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Models.Entities;

namespace TodoListApp2.Services;

public class OrganizationService : IOrganizationService
{
    private readonly AppDbContext _context;

    public OrganizationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<OrganizationDto>> GetOrganizationsForUserAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        bool isPlatformAdmin = user?.Role == "PlatformAdmin";

        var query = _context.Organizations
            .Include(o => o.Members)
            .Include(o => o.Workspaces)
                .ThenInclude(w => w.Tasks)
            .AsSplitQuery()
            .AsQueryable();

        if (!isPlatformAdmin)
        {
            // Non-platform admin users (including OrgAdmin) can ONLY see organizations where they are currently an active member
            query = query.Where(o => o.Members.Any(m => m.UserId == userId));
        }

        var orgs = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

        return orgs.Select(o => new OrganizationDto(
            o.Id,
            o.Name,
            o.Slug,
            o.Description,
            o.LogoUrl,
            o.SubscriptionPlan,
            o.MaxMembers,
            o.Members.Count,
            o.Workspaces.Count,
            o.Workspaces.SelectMany(w => w.Tasks).Count(),
            o.CreatedAt
        )).ToList();
    }

    public async Task<OrganizationDto?> GetOrganizationByIdAsync(int orgId, int userId = 0, bool isPlatformAdmin = false)
    {
        var o = await _context.Organizations
            .Include(o => o.Members)
            .Include(o => o.Workspaces)
                .ThenInclude(w => w.Tasks)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == orgId);

        if (o == null) return null;

        // If not platform admin, verify the caller is actually an enrolled member of THIS specific organization
        if (userId > 0 && !isPlatformAdmin)
        {
            bool isMember = o.Members.Any(m => m.UserId == userId);
            if (!isMember)
            {
                return null;
            }
        }

        return new OrganizationDto(
            o.Id,
            o.Name,
            o.Slug,
            o.Description,
            o.LogoUrl,
            o.SubscriptionPlan,
            o.MaxMembers,
            o.Members.Count,
            o.Workspaces.Count,
            o.Workspaces.SelectMany(w => w.Tasks).Count(),
            o.CreatedAt
        );
    }

    public async Task<OrganizationDto> CreateOrganizationAsync(int userId, CreateOrganizationRequest request)
    {
        var creator = await _context.Users.FindAsync(userId);
        if (creator == null)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        if (creator.Role == "PlatformAdmin")
        {
            throw new UnauthorizedAccessException("Platform Administrators are not allowed to register organizations. Only Organization Admins can register an organization.");
        }

        if (creator.Role != "OrgAdmin")
        {
            throw new UnauthorizedAccessException("Organizations can only be registered by Organization Administrators.");
        }

        string slug = request.Name.Trim().ToLowerInvariant().Replace(" ", "-").Replace("/", "-");
        int count = 1;
        string baseSlug = slug;
        while (await _context.Organizations.AnyAsync(o => o.Slug == slug))
        {
            slug = $"{baseSlug}-{count++}";
        }

        var org = new Organization
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description?.Trim(),
            SubscriptionPlan = request.SubscriptionPlan,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(org);
        await _context.SaveChangesAsync();

        _context.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = userId,
            Role = "OrgAdmin",
            Department = "Executive",
            JoinedAt = DateTime.UtcNow,
            Status = "Active"
        });
        await _context.SaveChangesAsync();

        return (await GetOrganizationByIdAsync(org.Id, userId, false))!;
    }

    public async Task<WorkspaceDto> RegisterOrganizationWorkspaceAsync(int orgId, int currentUserId)
    {
        var org = await _context.Organizations
            .Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == orgId);

        if (org == null)
            throw new ArgumentException("Organization not found.");

        var user = await _context.Users.FindAsync(currentUserId);
        if (user?.Role == "PlatformAdmin")
        {
            throw new UnauthorizedAccessException("Platform Administrators cannot register organization collaborative workspaces. This must be registered by an Organization Administrator.");
        }

        bool isOrgAdmin = org.Members.Any(m => m.UserId == currentUserId && m.Role == "OrgAdmin") ||
                          (org.CreatedByUserId == currentUserId && org.Members.Any(m => m.UserId == currentUserId));

        if (!isOrgAdmin)
        {
            throw new UnauthorizedAccessException("Only Organization Administrators of this organization are authorized to register and launch its workspaces.");
        }

        // 1. Provision or find Business workspace
        var bizWs = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.OrganizationId == orgId && w.Mode == "Business");

        if (bizWs == null)
        {
            bizWs = new Workspace
            {
                OrganizationId = orgId,
                Name = $"{org.Name} Operations",
                Description = "Organization-wide departments, approvals, and executive governance",
                Mode = "Business",
                Color = "#059669",
                CreatedByUserId = currentUserId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Workspaces.Add(bizWs);
            await _context.SaveChangesAsync();
        }

        bool hasBizMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == bizWs.Id && wm.UserId == currentUserId);
        if (!hasBizMember)
        {
            _context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = bizWs.Id,
                UserId = currentUserId,
                Role = "Admin",
                JoinedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        // 2. Provision or find Team workspace
        var teamWs = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.OrganizationId == orgId && w.Mode == "Team");

        if (teamWs == null)
        {
            teamWs = new Workspace
            {
                OrganizationId = orgId,
                Name = $"{org.Name} Team Board",
                Description = "Collaborative team workspace for project execution and task coordination",
                Mode = "Team",
                Color = "#0284c7",
                CreatedByUserId = currentUserId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Workspaces.Add(teamWs);
            await _context.SaveChangesAsync();
        }

        bool hasTeamMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == teamWs.Id && wm.UserId == currentUserId);
        if (!hasTeamMember)
        {
            _context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = teamWs.Id,
                UserId = currentUserId,
                Role = "Admin",
                JoinedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        // Ensure all active org members are in both workspaces
        var orgMembers = await _context.OrganizationMembers
            .Include(m => m.User)
            .Where(m => m.OrganizationId == orgId && m.User.Role != "PlatformAdmin")
            .ToListAsync();

        foreach (var member in orgMembers)
        {
            if (!await _context.WorkspaceMembers.AnyAsync(wm => wm.WorkspaceId == bizWs.Id && wm.UserId == member.UserId))
            {
                _context.WorkspaceMembers.Add(new WorkspaceMember
                {
                    WorkspaceId = bizWs.Id,
                    UserId = member.UserId,
                    Role = member.Role == "OrgAdmin" || member.Role == "Manager" ? "Admin" : "Member",
                    JoinedAt = DateTime.UtcNow
                });
            }

            if (!await _context.WorkspaceMembers.AnyAsync(wm => wm.WorkspaceId == teamWs.Id && wm.UserId == member.UserId))
            {
                _context.WorkspaceMembers.Add(new WorkspaceMember
                {
                    WorkspaceId = teamWs.Id,
                    UserId = member.UserId,
                    Role = member.Role == "OrgAdmin" || member.Role == "Manager" ? "Admin" : "Member",
                    JoinedAt = DateTime.UtcNow
                });
            }
        }
        await _context.SaveChangesAsync();

        return new WorkspaceDto(
            bizWs.Id,
            bizWs.OrganizationId,
            bizWs.Name,
            bizWs.Description,
            bizWs.Mode,
            bizWs.Color,
            bizWs.CreatedByUserId,
            0,
            0,
            0,
            orgMembers.Count,
            bizWs.CreatedAt
        );
    }

    public async Task<List<OrgMemberDto>> GetOrganizationMembersAsync(int orgId, int currentUserId = 0, bool isPlatformAdmin = false)
    {
        if (currentUserId > 0 && !isPlatformAdmin)
        {
            bool isMember = await _context.OrganizationMembers.AnyAsync(m => m.OrganizationId == orgId && m.UserId == currentUserId);
            if (!isMember)
            {
                return new List<OrgMemberDto>();
            }
        }

        var members = await _context.OrganizationMembers
            .AsNoTracking()
            .Include(m => m.User)
                .ThenInclude(u => u.AssignedTasks)
            .Where(m => m.OrganizationId == orgId && m.User.Role != "PlatformAdmin")
            .OrderBy(m => m.Department)
            .ThenBy(m => m.User.FullName)
            .ToListAsync();

        return members.Select(m => new OrgMemberDto(
            m.Id,
            m.OrganizationId,
            m.UserId,
            m.User.Username,
            m.User.FullName,
            m.User.Email,
            m.Role,
            m.Department,
            m.Status,
            m.User.AssignedTasks.Count(t => t.Status != "Completed"),
            m.JoinedAt
        )).ToList();
    }

    public async Task<OrgMemberDto?> AddOrganizationMemberAsync(int orgId, AddOrgMemberRequest request, int currentUserId, bool isPlatformAdmin = false)
    {
        var currentUser = await _context.Users.FindAsync(currentUserId);
        bool isManagerOrAdmin = isPlatformAdmin || currentUser?.Role == "OrgAdmin" || currentUser?.Role == "Manager";
        if (!isManagerOrAdmin)
        {
            throw new UnauthorizedAccessException("Only Managers and Administrators are permitted to add employees/members to an organization.");
        }

        var input = request.UsernameOrEmail.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == input || u.Email.ToLower() == input);
        if (user == null) return null;

        if (user.Role == "PlatformAdmin")
        {
            throw new InvalidOperationException("Platform Administrator cannot be added as an organization employee/member.");
        }

        var existing = await _context.OrganizationMembers.FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == user.Id);
        if (existing != null)
        {
            throw new InvalidOperationException($"User '{user.FullName ?? user.Username}' is already an enrolled member of this organization.");
        }

        var member = new OrganizationMember
        {
            OrganizationId = orgId,
            UserId = user.Id,
            Role = request.Role,
            Department = request.Department,
            JoinedAt = DateTime.UtcNow,
            Status = "Active"
        };

        _context.OrganizationMembers.Add(member);
        await _context.SaveChangesAsync();

        var orgWorkspaces = await _context.Workspaces.Where(w => w.OrganizationId == orgId).ToListAsync();
        foreach (var ws in orgWorkspaces)
        {
            bool hasMember = await _context.WorkspaceMembers.AnyAsync(wm => wm.WorkspaceId == ws.Id && wm.UserId == user.Id);
            if (!hasMember)
            {
                _context.WorkspaceMembers.Add(new WorkspaceMember
                {
                    WorkspaceId = ws.Id,
                    UserId = user.Id,
                    Role = request.Role == "OrgAdmin" || request.Role == "Manager" ? "Admin" : "Member",
                    JoinedAt = DateTime.UtcNow
                });
            }
        }
        await _context.SaveChangesAsync();

        return new OrgMemberDto(member.Id, member.OrganizationId, user.Id, user.Username, user.FullName, user.Email, member.Role, member.Department, member.Status, 0, member.JoinedAt);
    }

    public async Task<OrgMemberDto?> UpdateOrganizationMemberAsync(int orgId, int memberId, UpdateOrgMemberRequest request, int currentUserId, bool isPlatformAdmin = false)
    {
        var org = await _context.Organizations.FindAsync(orgId);
        if (org == null) return null;

        var currentUser = await _context.Users.FindAsync(currentUserId);
        bool isManagerOrAdmin = isPlatformAdmin || 
            currentUser?.Role == "PlatformAdmin" || 
            currentUser?.Role == "OrgAdmin" || 
            org.CreatedByUserId == currentUserId ||
            await _context.OrganizationMembers.AnyAsync(m => m.OrganizationId == orgId && m.UserId == currentUserId && (m.Role == "OrgAdmin" || m.Role == "Manager"));

        if (!isManagerOrAdmin)
        {
            throw new UnauthorizedAccessException("Only Organization Administrators and Managers are permitted to edit employee details.");
        }

        var member = await _context.OrganizationMembers
            .Include(m => m.User)
                .ThenInclude(u => u.AssignedTasks)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == orgId);

        if (member == null) return null;

        var targetUser = member.User;

        // Update Role if provided
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            string newRole = request.Role.Trim();
            if (newRole == "OrgOwner") newRole = "OrgAdmin";
            member.Role = newRole;

            // If the user's primary role was Manager or Employee, update their primary role if appropriate
            if (targetUser.Role != "PlatformAdmin")
            {
                if (newRole == "OrgAdmin" || newRole == "Manager" || newRole == "Employee")
                {
                    targetUser.Role = newRole;
                }
            }

            // Sync collaborative workspace roles
            var orgWorkspaces = await _context.Workspaces.Where(w => w.OrganizationId == orgId).ToListAsync();
            foreach (var ws in orgWorkspaces)
            {
                var wsMember = await _context.WorkspaceMembers.FirstOrDefaultAsync(wm => wm.WorkspaceId == ws.Id && wm.UserId == targetUser.Id);
                if (wsMember != null)
                {
                    wsMember.Role = (newRole == "OrgAdmin" || newRole == "Manager") ? "Admin" : "Member";
                }
            }
        }

        // Update Department if provided
        if (request.Department != null)
        {
            member.Department = string.IsNullOrWhiteSpace(request.Department) ? "General" : request.Department.Trim();
            targetUser.Department = member.Department;
        }

        // Update Status if provided
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            member.Status = request.Status.Trim();
        }

        await _context.SaveChangesAsync();

        int activeTasksCount = targetUser.AssignedTasks.Count(t => t.Status != "Completed");
        return new OrgMemberDto(
            member.Id,
            member.OrganizationId,
            targetUser.Id,
            targetUser.Username,
            targetUser.FullName,
            targetUser.Email,
            member.Role,
            member.Department,
            member.Status,
            activeTasksCount,
            member.JoinedAt
        );
    }

    public async Task<bool> RemoveOrganizationMemberAsync(int orgId, int memberId, int currentUserId, bool isPlatformAdmin = false)
    {
        var org = await _context.Organizations.FindAsync(orgId);
        if (org == null) return false;

        var currentUser = await _context.Users.FindAsync(currentUserId);
        bool isManagerOrAdmin = isPlatformAdmin || 
            currentUser?.Role == "PlatformAdmin" || 
            currentUser?.Role == "OrgAdmin" || 
            org.CreatedByUserId == currentUserId ||
            await _context.OrganizationMembers.AnyAsync(m => m.OrganizationId == orgId && m.UserId == currentUserId && (m.Role == "OrgAdmin" || m.Role == "Manager"));

        if (!isManagerOrAdmin)
        {
            return false;
        }

        var member = await _context.OrganizationMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == orgId);

        if (member == null) return false;

        int targetUserId = member.UserId;
        var targetUser = member.User;

        // Cannot remove the primary organization owner/admin if they are trying to delete self without transferring
        if (targetUserId == currentUserId && org.CreatedByUserId == currentUserId)
        {
            throw new InvalidOperationException("You cannot remove yourself as the primary Organization Administrator. Transfer adminship first or delete the organization.");
        }

        _context.OrganizationMembers.Remove(member);

        // Clean up workspace memberships in this organization's workspaces
        var orgWorkspaceIds = await _context.Workspaces.Where(w => w.OrganizationId == orgId).Select(w => w.Id).ToListAsync();
        var wsMembers = await _context.WorkspaceMembers.Where(wm => orgWorkspaceIds.Contains(wm.WorkspaceId) && wm.UserId == targetUserId).ToListAsync();
        _context.WorkspaceMembers.RemoveRange(wsMembers);

        // If target user is not in any other organization, reset their role to "User"
        bool hasOtherOrg = await _context.OrganizationMembers.AnyAsync(m => m.UserId == targetUserId && m.Id != member.Id);
        if (!hasOtherOrg && targetUser.Role != "PlatformAdmin")
        {
            targetUser.Role = "User";
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<string>> GetDepartmentsAsync(int orgId, int currentUserId = 0, bool isPlatformAdmin = false)
    {
        if (currentUserId > 0 && !isPlatformAdmin)
        {
            bool isMember = await _context.OrganizationMembers.AnyAsync(m => m.OrganizationId == orgId && m.UserId == currentUserId);
            if (!isMember)
            {
                return new List<string>();
            }
        }

        var depts = await _context.OrganizationMembers
            .Where(m => m.OrganizationId == orgId && !string.IsNullOrEmpty(m.Department))
            .Select(m => m.Department!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();

        if (depts.Count == 0)
        {
            depts = new List<string> { "Engineering", "Product", "Marketing", "Sales", "Operations", "HR" };
        }

        return depts;
    }

    public async Task<bool> DeleteOrganizationAsync(int orgId, int currentUserId, bool isPlatformAdmin = false)
    {
        var org = await _context.Organizations
            .Include(o => o.Members)
            .Include(o => o.Workspaces)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == orgId);

        if (org == null) return false;

        var user = await _context.Users.FindAsync(currentUserId);
        bool isAdmin = isPlatformAdmin || user?.Role == "PlatformAdmin" || user?.Role == "OrgAdmin" || org.CreatedByUserId == currentUserId;
        if (!isAdmin) return false;

        // 1. Remove access requests for this org
        var reqs = await _context.AccessRequests.Where(r => r.OrganizationId == orgId).ToListAsync();
        _context.AccessRequests.RemoveRange(reqs);

        // 2. Remove org members
        var members = await _context.OrganizationMembers.Where(m => m.OrganizationId == orgId).ToListAsync();
        _context.OrganizationMembers.RemoveRange(members);

        // 3. Clean up workspaces associated with org
        var wsList = await _context.Workspaces
            .Where(w => w.OrganizationId == orgId)
            .Include(w => w.Tasks)
                .ThenInclude(t => t.Comments)
            .Include(w => w.Tasks)
                .ThenInclude(t => t.Subtasks)
            .Include(w => w.Tasks)
                .ThenInclude(t => t.Approvals)
            .Include(w => w.Projects)
                .ThenInclude(p => p.Milestones)
            .Include(w => w.Goals)
            .Include(w => w.Members)
            .AsSplitQuery()
            .ToListAsync();

        foreach (var w in wsList)
        {
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
        }

        _context.Organizations.Remove(org);
        await _context.SaveChangesAsync();
        return true;
    }
}
