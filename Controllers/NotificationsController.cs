using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;

namespace BTL_LTWEB_QLChiTieu.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _context;

    public NotificationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out int id) ? id : 0;
    }

    [HttpGet]
    public async Task<IActionResult> GetUnread()
    {
        int userId = GetCurrentUserId();

        var notifs = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .Select(n => new
            {
                notificationId = n.NotificationId,
                title = n.Title,
                message = n.Message,
                alertLevel = n.AlertLevel,
                createdAt = n.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            })
            .ToListAsync();

        return Json(notifs);
    }

    [HttpPost]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        int userId = GetCurrentUserId();

        var notif = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

        if (notif != null)
        {
            notif.IsRead = true;
            await _context.SaveChangesAsync();
        }

        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllAsRead()
    {
        int userId = GetCurrentUserId();

        var unread = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.IsRead = true;
        }

        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }
}
