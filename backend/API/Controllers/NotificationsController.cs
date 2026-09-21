using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp2.Services;

namespace TodoListApp2.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] bool unreadOnly = false)
    {
        int userId = GetUserId();
        var notifs = await _notificationService.GetUserNotificationsAsync(userId, unreadOnly);
        return Ok(notifs);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        int userId = GetUserId();
        int count = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(new { unreadCount = count });
    }

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        int userId = GetUserId();
        bool ok = await _notificationService.MarkAsReadAsync(userId, id);
        if (!ok) return NotFound(new { message = "Notification not found." });
        return Ok(new { message = "Notification marked as read." });
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        int userId = GetUserId();
        await _notificationService.MarkAllAsReadAsync(userId);
        return Ok(new { message = "All notifications marked as read." });
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> ClearNotifications()
    {
        int userId = GetUserId();
        await _notificationService.ClearNotificationsAsync(userId);
        return Ok(new { message = "All notifications cleared." });
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }
}
