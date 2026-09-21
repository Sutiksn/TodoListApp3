using Microsoft.EntityFrameworkCore;
using TodoListApp2.Data;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Models.Entities;

namespace TodoListApp2.Services;

public class AccessRequestService : IAccessRequestService
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly IActivityService _activityService;

    public AccessRequestService(
        AppDbContext context,
        IEmailService emailService,
        INotificationService notificationService,
        IActivityService activityService)
    {
        _context = context;
        _emailService = emailService;
        _notificationService = notificationService;
        _activityService = activityService;
    }

    public async Task<List<AccessRequestDto>> GetIncomingRequestsAsync(int userId, string userRole)
    {
        var query = _context.AccessRequests
            .Include(r => r.User)
            .Include(r => r.Organization)
            .Include(r => r.TargetManagerUser)
            .Include(r => r.DecidedByUser)
            .Where(r => r.Status == "Pending")
            .AsQueryable();

        if (userRole == "PlatformAdmin")
        {
            // Platform Admin ONLY approves OrgAdmin requests. Manager and Employee approvals pass over to OrgAdmin
            query = query.Where(r => r.RequestedRole == "OrgAdmin");
        }
        else if (userRole == "OrgAdmin")
        {
            // OrgAdmin handles Manager and Employee requests for their organization
            var myOrgIds = await _context.OrganizationMembers
                .Where(m => m.UserId == userId && m.Role == "OrgAdmin")
                .Select(m => m.OrganizationId)
                .ToListAsync();

            query = query.Where(r => (r.RequestedRole == "Manager" || r.RequestedRole == "Employee") && r.OrganizationId.HasValue && myOrgIds.Contains(r.OrganizationId.Value));
        }
        else if (userRole == "Manager")
        {
            // Manager handles Employee requests for their organization or targeted to them
            var myOrgIds = await _context.OrganizationMembers
                .Where(m => m.UserId == userId && m.Role == "Manager")
                .Select(m => m.OrganizationId)
                .ToListAsync();

            query = query.Where(r => r.RequestedRole == "Employee" && (
                r.TargetManagerUserId == userId || 
                (r.OrganizationId.HasValue && myOrgIds.Contains(r.OrganizationId.Value))
            ));
        }
        else
        {
            return new List<AccessRequestDto>();
        }

        var list = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

        return list.Select(MapToDto).ToList();
    }

    public async Task<List<AccessRequestDto>> GetMyRequestsAsync(int userId)
    {
        var list = await _context.AccessRequests
            .Include(r => r.User)
            .Include(r => r.Organization)
            .Include(r => r.TargetManagerUser)
            .Include(r => r.DecidedByUser)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return list.Select(MapToDto).ToList();
    }

    public async Task<List<AccessRequestDto>> GetApprovedRequestsAsync(int userId, string userRole)
    {
        var query = _context.AccessRequests
            .Include(r => r.User)
            .Include(r => r.Organization)
            .Include(r => r.TargetManagerUser)
            .Include(r => r.DecidedByUser)
            .Where(r => r.Status == "Approved" || r.Status == "Rejected")
            .AsQueryable();

        if (userRole == "PlatformAdmin")
        {
            query = query.Where(r => r.RequestedRole == "OrgAdmin" || r.DecidedByUserId == userId);
        }
        else if (userRole == "OrgAdmin")
        {
            var myOrgIds = await _context.OrganizationMembers
                .Where(m => m.UserId == userId && m.Role == "OrgAdmin")
                .Select(m => m.OrganizationId)
                .ToListAsync();

            query = query.Where(r => r.DecidedByUserId == userId || (r.OrganizationId.HasValue && myOrgIds.Contains(r.OrganizationId.Value)));
        }
        else
        {
            query = query.Where(r => r.DecidedByUserId == userId || r.UserId == userId);
        }

        var list = await query.OrderByDescending(r => r.DecidedAt ?? r.CreatedAt).ToListAsync();
        return list.Select(MapToDto).ToList();
    }

    public async Task<AccessRequestDto> CreateRequestAsync(int userId, CreateAccessRequestDto request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new ArgumentException("User account not found.");

        string requestedRole = request.RequestedRole == "OrgOwner" ? "OrgAdmin" : request.RequestedRole;

        // Check if user already belongs to an organization
        var existingUserOrg = await _context.OrganizationMembers
            .Include(m => m.Organization)
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (existingUserOrg != null)
        {
            if (request.OrganizationId.HasValue && request.OrganizationId.Value != existingUserOrg.OrganizationId)
            {
                throw new InvalidOperationException($"You already belong to organization '{existingUserOrg.Organization.Name}'. You cannot apply for a role under another organization.");
            }
            if (requestedRole == "OrgAdmin" && !string.IsNullOrWhiteSpace(request.OrganizationName) &&
                !string.Equals(request.OrganizationName.Trim(), existingUserOrg.Organization.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"You already belong to organization '{existingUserOrg.Organization.Name}'. You cannot apply for another organization.");
            }

            request = request with { OrganizationId = existingUserOrg.OrganizationId, RequestedRole = requestedRole };
        }
        else
        {
            request = request with { RequestedRole = requestedRole };
        }

        if (request.RequestedRole == "OrgAdmin" && string.IsNullOrWhiteSpace(request.OrganizationName) && !request.OrganizationId.HasValue)
        {
            throw new ArgumentException("Organization Name is required to request Organization Admin access.");
        }

        var req = new AccessRequest
        {
            UserId = userId,
            RequestedRole = request.RequestedRole,
            OrganizationId = request.OrganizationId,
            ProposedOrganizationName = request.RequestedRole == "OrgAdmin" ? request.OrganizationName?.Trim() : null,
            TargetManagerUserId = request.TargetManagerUserId,
            Comments = request.Comments?.Trim(),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.AccessRequests.Add(req);
        await _context.SaveChangesAsync();

        // If requested OrgAdmin, notify platform admin
        if (req.RequestedRole == "OrgAdmin")
        {
            var platformAdmin = await _context.Users.FirstOrDefaultAsync(u => u.Role == "PlatformAdmin");
            if (platformAdmin != null)
            {
                try
                {
                    await _emailService.SendPlatformAdminNotificationAsync(
                        platformAdmin.Email,
                        user.FullName,
                        user.Email,
                        req.RequestedRole
                    );
                }
                catch
                {
                    // Fallback if SMTP error
                }

                await _notificationService.CreateNotificationAsync(
                    platformAdmin.Id,
                    $"New {req.RequestedRole} Request",
                    $"{user.FullName} ({user.Email}) has requested {req.RequestedRole} privileges.",
                    "/access-requests",
                    "RoleRequest"
                );
            }
        }
        else if (req.RequestedRole == "Manager" && req.OrganizationId.HasValue)
        {
            var orgAdmins = await _context.OrganizationMembers
                .Where(m => m.OrganizationId == req.OrganizationId.Value && m.Role == "OrgAdmin")
                .Select(m => m.UserId)
                .ToListAsync();

            foreach (var adminId in orgAdmins)
            {
                await _notificationService.CreateNotificationAsync(
                    adminId,
                    "New Manager Request",
                    $"{user.FullName} has requested to become a Manager for your organization.",
                    "/access-requests",
                    "RoleRequest"
                );
            }
        }
        else if (req.RequestedRole == "Employee")
        {
            if (req.TargetManagerUserId.HasValue)
            {
                await _notificationService.CreateNotificationAsync(
                    req.TargetManagerUserId.Value,
                    "New Employee Access Request",
                    $"{user.FullName} has requested to join your team as an Employee.",
                    "/access-requests",
                    "RoleRequest"
                );
            }
        }

        await _activityService.LogAsync(
            req.OrganizationId,
            null,
            null,
            null,
            userId,
            user.Username,
            "AccessRequestCreated",
            $"Submitted request for role '{req.RequestedRole}'",
            "Personal"
        );

        return (await GetRequestByIdAsync(req.Id))!;
    }

    public async Task<AccessRequestDto?> DecideRequestAsync(int deciderUserId, string deciderUsername, int requestId, DecideAccessRequestDto decision)
    {
        var req = await _context.AccessRequests
            .Include(r => r.User)
            .Include(r => r.Organization)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (req == null) return null;
        if (req.Status != "Pending") return MapToDto(req);

        var decider = await _context.Users.FindAsync(deciderUserId);
        if (decider == null) throw new UnauthorizedAccessException("Decider account not found.");

        if (decider.Role == "PlatformAdmin")
        {
            // Platform Admin can ONLY approve or reject OrgAdmin requests
            if (req.RequestedRole != "OrgAdmin")
            {
                throw new InvalidOperationException("Platform Admin can only approve Organization Admin requests. Manager and Employee requests must be approved by their respective Organization Admin.");
            }
        }
        else if (decider.Role == "OrgAdmin")
        {
            var myOrgIds = await _context.OrganizationMembers
                .Where(m => m.UserId == deciderUserId && m.Role == "OrgAdmin")
                .Select(m => m.OrganizationId)
                .ToListAsync();

            if (!req.OrganizationId.HasValue || !myOrgIds.Contains(req.OrganizationId.Value))
            {
                throw new UnauthorizedAccessException("You can only decide access requests for your organization.");
            }
        }
        else if (decider.Role == "Manager")
        {
            var myOrgIds = await _context.OrganizationMembers
                .Where(m => m.UserId == deciderUserId && m.Role == "Manager")
                .Select(m => m.OrganizationId)
                .ToListAsync();

            bool isTargetManager = req.TargetManagerUserId == deciderUserId;
            bool isOrgMatch = req.OrganizationId.HasValue && myOrgIds.Contains(req.OrganizationId.Value);

            if (req.RequestedRole != "Employee" || (!isTargetManager && !isOrgMatch))
            {
                throw new UnauthorizedAccessException("Managers can only decide Employee requests for their team.");
            }
        }
        else
        {
            throw new UnauthorizedAccessException("You do not have permission to decide access requests.");
        }

        bool isApproved = (decision.Approved == true) ||
            (!string.IsNullOrWhiteSpace(decision.Action) && decision.Action.Equals("Approve", StringComparison.OrdinalIgnoreCase));
        req.Status = isApproved ? "Approved" : "Rejected";
        req.DecidedByUserId = deciderUserId;
        req.DecidedAt = DateTime.UtcNow;

        if (isApproved)
        {
            var user = req.User;
            if (req.RequestedRole == "OrgAdmin")
            {
                if (user.Role != "PlatformAdmin")
                {
                    user.Role = "OrgAdmin";
                }

                int orgId;
                if (!req.OrganizationId.HasValue)
                {
                    string orgName = !string.IsNullOrWhiteSpace(req.ProposedOrganizationName) 
                        ? req.ProposedOrganizationName.Trim() 
                        : $"{user.FullName}'s Enterprise";

                    var existingOrg = await _context.Organizations
                        .Include(o => o.Members)
                        .Include(o => o.Workspaces)
                            .ThenInclude(w => w.Members)
                        .FirstOrDefaultAsync(o => o.Name.ToLower() == orgName.ToLower());

                    if (existingOrg != null)
                    {
                        int oldOwnerId = existingOrg.CreatedByUserId;
                        existingOrg.CreatedByUserId = user.Id;
                        orgId = existingOrg.Id;
                        req.OrganizationId = existingOrg.Id;

                        var existingMember = existingOrg.Members.FirstOrDefault(m => m.UserId == user.Id);
                        if (existingMember != null)
                        {
                            existingMember.Role = "OrgAdmin";
                            existingMember.Status = "Active";
                        }
                        else
                        {
                            _context.OrganizationMembers.Add(new OrganizationMember
                            {
                                OrganizationId = existingOrg.Id,
                                UserId = user.Id,
                                Role = "OrgAdmin",
                                Department = user.Department ?? "Executive",
                                JoinedAt = DateTime.UtcNow,
                                Status = "Active"
                            });
                        }

                        if (oldOwnerId != 0 && oldOwnerId != user.Id)
                        {
                            var prevOwner = await _context.Users.FindAsync(oldOwnerId);
                            if (prevOwner != null && prevOwner.Role == "OrgAdmin") prevOwner.Role = "Manager";
                            var prevMember = existingOrg.Members.FirstOrDefault(m => m.UserId == oldOwnerId);
                            if (prevMember != null && prevMember.Role == "OrgAdmin") prevMember.Role = "Manager";
                        }

                        var collabWorkspaces = existingOrg.Workspaces.Where(w => w.Mode == "Business" || w.Mode == "Team").ToList();
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
                        }
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        string baseSlug = System.Text.RegularExpressions.Regex.Replace(orgName.ToLower(), @"[^a-z0-9]+", "-").Trim('-');
                        if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = $"{user.Username.ToLower()}-org";
                        string uniqueSlug = baseSlug;
                        int counter = 1;
                        while (await _context.Organizations.AnyAsync(o => o.Slug == uniqueSlug))
                        {
                            uniqueSlug = $"{baseSlug}-{counter++}";
                        }

                        var org = new Organization
                        {
                            Name = orgName,
                            Slug = uniqueSlug,
                            Description = $"Enterprise organization managed by {user.FullName}",
                            CreatedByUserId = user.Id,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Organizations.Add(org);
                        await _context.SaveChangesAsync();

                        req.OrganizationId = org.Id;
                        orgId = org.Id;

                        _context.OrganizationMembers.Add(new OrganizationMember
                        {
                            OrganizationId = org.Id,
                            UserId = user.Id,
                            Role = "OrgAdmin",
                            Department = "Executive",
                            JoinedAt = DateTime.UtcNow,
                            Status = "Active"
                        });
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    orgId = req.OrganizationId.Value;
                    var existingOrg = await _context.Organizations
                        .Include(o => o.Members)
                        .Include(o => o.Workspaces)
                            .ThenInclude(w => w.Members)
                        .AsSplitQuery()
                        .FirstOrDefaultAsync(o => o.Id == orgId);

                    if (existingOrg != null)
                    {
                        existingOrg.CreatedByUserId = user.Id;
                        var existingMember = existingOrg.Members.FirstOrDefault(m => m.UserId == user.Id);
                        if (existingMember != null)
                        {
                            existingMember.Role = "OrgAdmin";
                            existingMember.Status = "Active";
                        }
                        else
                        {
                            _context.OrganizationMembers.Add(new OrganizationMember
                            {
                                OrganizationId = orgId,
                                UserId = user.Id,
                                Role = "OrgAdmin",
                                Department = "Executive",
                                JoinedAt = DateTime.UtcNow,
                                Status = "Active"
                            });
                        }

                        var collabWorkspaces = existingOrg.Workspaces.Where(w => w.Mode == "Business" || w.Mode == "Team").ToList();
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
                        }
                        await _context.SaveChangesAsync();
                    }
                }
            }
            else if (req.RequestedRole == "Manager")
            {
                if (user.Role != "PlatformAdmin")
                {
                    user.Role = "Manager";
                }

                if (req.OrganizationId.HasValue)
                {
                    var existingMember = await _context.OrganizationMembers
                        .FirstOrDefaultAsync(m => m.OrganizationId == req.OrganizationId.Value && m.UserId == user.Id);

                    if (existingMember != null)
                    {
                        existingMember.Role = "Manager";
                    }
                    else
                    {
                        _context.OrganizationMembers.Add(new OrganizationMember
                        {
                            OrganizationId = req.OrganizationId.Value,
                            UserId = user.Id,
                            Role = "Manager",
                            Department = "Management",
                            JoinedAt = DateTime.UtcNow,
                            Status = "Active"
                        });
                    }

                    // Add to org workspaces as Admin
                    var orgWorkspaces = await _context.Workspaces.Where(w => w.OrganizationId == req.OrganizationId.Value).ToListAsync();
                    foreach (var ws in orgWorkspaces)
                    {
                        bool hasMember = await _context.WorkspaceMembers.AnyAsync(wm => wm.WorkspaceId == ws.Id && wm.UserId == user.Id);
                        if (!hasMember)
                        {
                            _context.WorkspaceMembers.Add(new WorkspaceMember
                            {
                                WorkspaceId = ws.Id,
                                UserId = user.Id,
                                Role = "Admin",
                                JoinedAt = DateTime.UtcNow
                            });
                        }
                    }
                }
            }
            else if (req.RequestedRole == "Employee")
            {
                if (user.Role != "PlatformAdmin")
                {
                    user.Role = "Employee";
                }

                int? orgId = req.OrganizationId;
                if (!orgId.HasValue && req.TargetManagerUserId.HasValue)
                {
                    var managerMember = await _context.OrganizationMembers
                        .FirstOrDefaultAsync(m => m.UserId == req.TargetManagerUserId.Value);
                    orgId = managerMember?.OrganizationId;
                }

                if (orgId.HasValue)
                {
                    var existingMember = await _context.OrganizationMembers
                        .FirstOrDefaultAsync(m => m.OrganizationId == orgId.Value && m.UserId == user.Id);

                    if (existingMember == null)
                    {
                        _context.OrganizationMembers.Add(new OrganizationMember
                        {
                            OrganizationId = orgId.Value,
                            UserId = user.Id,
                            Role = "Employee",
                            Department = "Operations",
                            JoinedAt = DateTime.UtcNow,
                            Status = "Active"
                        });
                    }

                    // Add to org collaborative workspaces as Member
                    var orgWorkspaces = await _context.Workspaces.Where(w => w.OrganizationId == orgId.Value).ToListAsync();
                    foreach (var ws in orgWorkspaces)
                    {
                        bool hasMember = await _context.WorkspaceMembers.AnyAsync(wm => wm.WorkspaceId == ws.Id && wm.UserId == user.Id);
                        if (!hasMember)
                        {
                            _context.WorkspaceMembers.Add(new WorkspaceMember
                            {
                                WorkspaceId = ws.Id,
                                UserId = user.Id,
                                Role = "Member",
                                JoinedAt = DateTime.UtcNow
                            });
                        }
                    }
                }
            }
        }

        await _context.SaveChangesAsync();

        // Notify requester
        await _notificationService.CreateNotificationAsync(
            req.UserId,
            $"Access Request {req.Status}",
            $"Your request for '{req.RequestedRole}' role has been {req.Status.ToLower()} by {deciderUsername}.",
            "/access-requests",
            "RequestDecided"
        );

        await _activityService.LogAsync(
            req.OrganizationId,
            null,
            null,
            null,
            deciderUserId,
            deciderUsername,
            "AccessRequestDecided",
            $"{deciderUsername} {req.Status.ToLower()} {req.User.Username}'s request for '{req.RequestedRole}'",
            "Admin"
        );

        return await GetRequestByIdAsync(requestId);
    }

    public async Task<List<OrgWithManagersDto>> GetOrganizationsWithManagersAsync()
    {
        var orgs = await _context.Organizations
            .Include(o => o.Members)
                .ThenInclude(m => m.User)
            .ToListAsync();

        return orgs.Select(o => new OrgWithManagersDto(
            o.Id,
            o.Name,
            o.Description,
            o.Members
                .Where(m => m.Role == "Manager" || m.Role == "OrgAdmin")
                .Select(m => new UserSummaryDto(
                    m.User.Id,
                    m.User.Username,
                    m.User.FullName,
                    m.User.Email,
                    m.Role,
                    m.Department
                ))
                .ToList()
        )).ToList();
    }

    private async Task<AccessRequestDto?> GetRequestByIdAsync(int id)
    {
        var r = await _context.AccessRequests
            .Include(r => r.User)
            .Include(r => r.Organization)
            .Include(r => r.TargetManagerUser)
            .Include(r => r.DecidedByUser)
            .FirstOrDefaultAsync(r => r.Id == id);

        return r != null ? MapToDto(r) : null;
    }

    private static AccessRequestDto MapToDto(AccessRequest r)
    {
        return new AccessRequestDto(
            r.Id,
            r.UserId,
            r.User?.Username ?? "user",
            r.User?.FullName ?? "User",
            r.User?.Email ?? "user@example.com",
            r.RequestedRole,
            r.OrganizationId,
            r.Organization?.Name ?? r.ProposedOrganizationName,
            r.ProposedOrganizationName,
            r.TargetManagerUserId,
            r.TargetManagerUser?.FullName ?? r.TargetManagerUser?.Username,
            r.Status,
            r.Comments,
            r.DecidedByUserId,
            r.DecidedByUser?.FullName ?? r.DecidedByUser?.Username,
            r.DecidedAt,
            r.CreatedAt
        );
    }
}
