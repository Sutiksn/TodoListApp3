using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Services;

namespace TodoListApp2.Controllers.Api;

[ApiController]
[Route("api/access-requests")]
[Authorize]
public class AccessRequestsController : ControllerBase
{
    private readonly IAccessRequestService _accessRequestService;

    public AccessRequestsController(IAccessRequestService accessRequestService)
    {
        _accessRequestService = accessRequestService;
    }

    [HttpGet("incoming")]
    public async Task<IActionResult> GetIncomingRequests()
    {
        int userId = GetUserId();
        string userRole = GetUserRole();
        var requests = await _accessRequestService.GetIncomingRequestsAsync(userId, userRole);
        return Ok(requests);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyRequests()
    {
        int userId = GetUserId();
        var requests = await _accessRequestService.GetMyRequestsAsync(userId);
        return Ok(requests);
    }

    [HttpGet("approved")]
    public async Task<IActionResult> GetApprovedRequests()
    {
        int userId = GetUserId();
        string userRole = GetUserRole();
        var requests = await _accessRequestService.GetApprovedRequestsAsync(userId, userRole);
        return Ok(requests);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRequest([FromBody] CreateAccessRequestDto request)
    {
        int userId = GetUserId();
        try
        {
            var res = await _accessRequestService.CreateRequestAsync(userId, request);
            return Ok(res);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/decide")]
    public async Task<IActionResult> DecideRequest(int id, [FromBody] DecideAccessRequestDto decision)
    {
        int userId = GetUserId();
        string username = GetUsername();

        var res = await _accessRequestService.DecideRequestAsync(userId, username, id, decision);
        if (res == null) return NotFound(new { message = "Access request not found or not permitted to decide." });

        return Ok(res);
    }

    [AllowAnonymous]
    [HttpGet("organizations-with-managers")]
    public async Task<IActionResult> GetOrganizationsWithManagers()
    {
        var orgs = await _accessRequestService.GetOrganizationsWithManagersAsync();
        return Ok(orgs);
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }

    private string GetUsername()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value ?? "user";
    }

    private string GetUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
    }
}
