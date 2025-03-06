using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VideoActive.Models;

[Route("api/connections")]
[ApiController]
public class ConnectionController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly AuthService _authService;

    public ConnectionController(ApplicationDbContext context, AuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    // GET: api/connections/contacts
    [HttpGet("contacts")]
    public async Task<IActionResult> GetUserContacts()
    {
        // ✅ Use AuthService to extract user from token
        var user = await _authService.GetUserFromToken(Request.Headers["Authorization"].ToString());
        if (user == null)
            return Unauthorized(new { message = "Invalid or expired token" });

        // ✅ Fetch all accepted contacts for this user
        var contacts = await _context.Relationships
            .Where(r => (r.UserId == user.UID || r.FriendId == user.UID) && r.Status == RelationshipStatus.Accepted)
            .Select(r => new
            {
                ContactId = r.UserId == user.UID ? r.FriendId : r.UserId,
                ContactName = r.UserId == user.UID ? r.Friend.Username : r.User.Username
            })
            .ToListAsync();

        return Ok(
            new {
                message = "success",
                contacts
                });
    }
}
