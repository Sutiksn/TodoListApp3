using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Services;

namespace TodoListApp2.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _orgService;

    public OrganizationsController(IOrganizationService orgService)
    {
        _orgService = orgService;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrganizations()
    {
        int userId = GetUserId();
        var orgs = await _orgService.GetOrganizationsForUserAsync(userId);
        return Ok(orgs);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrganizationById(int id)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = User.IsInRole("PlatformAdmin");
        var org = await _orgService.GetOrganizationByIdAsync(id, userId, isPlatformAdmin);
        if (org == null) return NotFound(new { message = "Organization not found." });
        return Ok(org);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrganization([FromBody] CreateOrganizationRequest request)
    {
        if (User.IsInRole("PlatformAdmin"))
        {
            return StatusCode(403, new { message = "Platform Administrators are not allowed to register organizations. Only Organization Admins can register an organization." });
        }
        int userId = GetUserId();
        try
        {
            var org = await _orgService.CreateOrganizationAsync(userId, request);
            return CreatedAtAction(nameof(GetOrganizationById), new { id = org.Id }, org);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/register-workspace")]
    public async Task<IActionResult> RegisterWorkspace(int id)
    {
        if (User.IsInRole("PlatformAdmin"))
        {
            return StatusCode(403, new { message = "Platform Administrators cannot register organization collaborative workspaces. This must be registered by an Organization Administrator." });
        }
        int userId = GetUserId();
        try
        {
            var ws = await _orgService.RegisterOrganizationWorkspaceAsync(id, userId);
            return Ok(new { message = "Organization collaborative workspaces registered and provisioned successfully!", workspace = ws });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetMembers(int id)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = User.IsInRole("PlatformAdmin");
        var members = await _orgService.GetOrganizationMembersAsync(id, userId, isPlatformAdmin);
        return Ok(members);
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(int id, [FromBody] AddOrgMemberRequest request)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = User.IsInRole("PlatformAdmin");
        try
        {
            var member = await _orgService.AddOrganizationMemberAsync(id, request, userId, isPlatformAdmin);
            if (member == null) return NotFound(new { message = "User not found to add to organization." });
            return Ok(member);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}/members/{memberId}")]
    [HttpPatch("{id}/members/{memberId}")]
    public async Task<IActionResult> UpdateMember(int id, int memberId, [FromBody] UpdateOrgMemberRequest request)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = User.IsInRole("PlatformAdmin");
        try
        {
            var updated = await _orgService.UpdateOrganizationMemberAsync(id, memberId, request, userId, isPlatformAdmin);
            if (updated == null) return NotFound(new { message = "Member not found in this organization or you are not authorized to edit." });
            return Ok(new { message = "Employee updated successfully!", member = updated });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}/members/{memberId}")]
    public async Task<IActionResult> RemoveMember(int id, int memberId)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = User.IsInRole("PlatformAdmin");
        try
        {
            bool removed = await _orgService.RemoveOrganizationMemberAsync(id, memberId, userId, isPlatformAdmin);
            if (!removed) return BadRequest(new { message = "Member not found or you are not authorized to remove members." });
            return Ok(new { message = "Employee removed from organization successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrganization(int id)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = User.IsInRole("PlatformAdmin");
        bool deleted = await _orgService.DeleteOrganizationAsync(id, userId, isPlatformAdmin);
        if (!deleted) return BadRequest(new { message = "Organization not found or you are not authorized to delete this organization." });
        return Ok(new { message = "Organization deleted successfully." });
    }

    [HttpGet("{id}/departments")]
    public async Task<IActionResult> GetDepartments(int id)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = User.IsInRole("PlatformAdmin");
        var depts = await _orgService.GetDepartmentsAsync(id, userId, isPlatformAdmin);
        return Ok(depts);
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }
}
