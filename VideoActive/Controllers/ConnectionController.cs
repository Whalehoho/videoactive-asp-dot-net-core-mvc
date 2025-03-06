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
    [HttpGet("getContacts")]
    public async Task<IActionResult> GetUserContacts()
    {
        // ✅ Use AuthService to extract user from token
        var user = await _authService.GetUserFromToken(Request.Headers["Authorization"].ToString());
        if (user == null)
            return Unauthorized(new { message = "error", details ="Invalid or expired token" });

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

    [HttpPost("addContact")]
    public async Task<IActionResult> AddContact([FromBody] AddContactRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "error", details = "Invalid request." });

        var user = await _authService.GetUserFromToken(Request.Headers["Authorization"].ToString());
        if (user == null)
            return Unauthorized(new { message = "error", details ="Invalid or expired token" });

        if (request.FriendId == user.UID)
            return BadRequest(new { message = "success", details ="You cannot add yourself as a contact." });

        // ✅ Check if the relationship already exists (both directions)
        var existingRelationship = await _context.Relationships.FirstOrDefaultAsync(r =>
            (r.UserId == user.UID && r.FriendId == request.FriendId) ||
            (r.UserId == request.FriendId && r.FriendId == user.UID)
        );

        if (existingRelationship != null)
        {
            if (existingRelationship.Status == RelationshipStatus.Pending)
                return BadRequest(new { message = "success", details = "Contact request already pending." });

            if (existingRelationship.Status == RelationshipStatus.Accepted)
                return BadRequest(new { message = "success", details = "You are already connected." });
        }

        // ✅ If no existing relationship, create a new one
        var newRelationship = new Relationship
        {
            UserId = user.UID,
            FriendId = request.FriendId,
            Status = RelationshipStatus.Pending
        };

        _context.Relationships.Add(newRelationship);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, new { message = "Database error occurred while saving. Please try again.", error = ex.Message });
        }

        return Ok(new { message = "success", details ="Contact request sent successfully." });
    }
    // ✅ Request model for adding contact
    public class AddContactRequest
    {
        public int FriendId { get; set; }
    }

    [HttpPost("acceptContact")]
    public async Task<IActionResult> AcceptContact([FromBody] AddContactRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "error", details = "Invalid request." });

        var user = await _authService.GetUserFromToken(Request.Headers["Authorization"].ToString());
        if (user == null)
            return Unauthorized(new { message = "error", details = "Invalid or expired token" });

        // ✅ Find the existing pending relationship
        var relationship = await _context.Relationships.FirstOrDefaultAsync(r =>
            r.UserId == request.FriendId && r.FriendId == user.UID && r.Status == RelationshipStatus.Pending
        );

        if (relationship == null)
            return NotFound(new { message = "success" , details = "No pending friend request found." });

        // ✅ Update status to Accepted
        relationship.Status = RelationshipStatus.Accepted;

        try
        {
            await _context.SaveChangesAsync();
            return Ok(new { message = "Friend request accepted successfully." });
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, new { message = "error", details = "Database error while updating relationship.", error = ex.Message });
        }
    }

    [HttpPost("rejectContact")]
    public async Task<IActionResult> RejectContact([FromBody] AddContactRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "error" , details ="Invalid request body." });

        var user = await _authService.GetUserFromToken(Request.Headers["Authorization"].ToString());
        if (user == null)
            return Unauthorized(new { message = "error" , details = "Invalid or expired token" });

        // ✅ Find the existing pending relationship
        var relationship = await _context.Relationships.FirstOrDefaultAsync(r =>
            r.UserId == request.FriendId && r.FriendId == user.UID && r.Status == RelationshipStatus.Pending
        );

        if (relationship == null)
            return NotFound(new { message = "error", details = "No pending friend request found." });

        // ✅ Remove the relationship from the database
        _context.Relationships.Remove(relationship);

        try
        {
            await _context.SaveChangesAsync();
            return Ok(new { message = "success" , details = "Friend request rejected successfully." });
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, new { message = "error" , details= "Database error while deleting relationship.", error = ex.Message });
        }
    }   
}
