using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using VideoActive.Models;
using System.Text.Json;

[Route("api/message")]
[ApiController]

public class MessageController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly AuthService _authService;

    public MessageController(ApplicationDbContext context, AuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    [HttpPost("addMessage")]
    public async Task<IActionResult> AddMessage([FromBody] AddMessageRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "error", details = "Invalid request." });

        var user = await _authService.GetUserFromHeader(Request.Headers["Authorization"].ToString());
        if (user == null)
            return Unauthorized(new { message = "error", details = "Invalid or expired token" });

        if (request.SenderId != user.UID)
            return Unauthorized(new { message = "error", details = "Invalid sender." });

        var receiver = await _context.Users.FirstOrDefaultAsync(u => u.UID == request.ReceiverId);
        if (receiver == null)
            return BadRequest(new { message = "error", details = "Invalid receiver." });

        var chatbox = await _context.Chatboxes
            .FirstOrDefaultAsync(c => (c.UserId1 == user.UID && c.UserId2 == receiver.UID) || (c.UserId1 == receiver.UID && c.UserId2 == user.UID));
        if (chatbox == null)
        {
            chatbox = new Chatbox
            {
                UserId1 = user.UID,
                UserId2 = receiver.UID
            };
            _context.Chatboxes.Add(chatbox);
            await _context.SaveChangesAsync();
        }

        var message = new Message
        {
            SenderId = user.UID,
            ReceiverId = receiver.UID,
            CID = chatbox.CID,
            MessageText = request.MessageText
        };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        return Ok(new { message = "success", details = "Message added." });
    }

    [HttpGet("getAllMessages")]
    public async Task<IActionResult> GetAllMessages()
    {
        var user = await _authService.GetUserFromHeader(Request.Headers["Authorization"].ToString());
        if (user == null)
            return Unauthorized(new { message = "error", details = "Invalid or expired token" });


        var messages = await _context.Messages
        .Where(m => m.SenderId == user.UID || m.ReceiverId == user.UID)
        .OrderBy(m => m.CreatedAt)
        .Join(_context.Users, m => m.SenderId, u => u.UID, (m, u) => new
        {
            m.MID,
            m.SenderId,
            SenderName = u.Username, // Fetch the Username of the sender
            m.ReceiverId,
            m.MessageText,
            m.CreatedAt
        })
        .ToListAsync();


        return Ok(new { message = "success", messages });
    }


    
    

}

public class AddMessageRequest
{
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    public string? MessageText { get; set; }
}
