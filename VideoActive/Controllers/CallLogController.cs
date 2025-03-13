using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using VideoActive.Models;
using System.Text.Json;

[Route("api/callLog")]
[ApiController]

public class CallLogController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly AuthService _authService;

    public CallLogController(ApplicationDbContext context, AuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    [HttpPost("startCall")]
    public async Task<IActionResult> StartCall([FromBody] AddStartCallRequest request)
    {
        // print callerId, calleeId, callType
        Console.WriteLine($"CallerId: {request.CallerId}, CalleeId: {request.CalleeId}, CallType: {request.CallType}");
        if (request == null)
            return BadRequest(new { message = "error", details = "Invalid request." });

        var user = await _authService.GetUserFromHeader(Request.Headers["Authorization"].ToString());
        // print user
        Console.WriteLine($"UserId: {user?.UID}");
        if (user == null)
            return Unauthorized(new { message = "error", details = "Invalid or expired token" });

        if (request.CallerId != user.UID){
            Console.WriteLine("Invalid caller");
            // print type of request.CallerId
            Console.WriteLine($"Type of request.CallerId: {request.CallerId.GetType()}");
            // print type of user.UID
            Console.WriteLine($"Type of user.UID: {user.UID.GetType()}");
            return Unauthorized(new { message = "error", details = "Invalid caller." });
        }

        var callee = await _context.Users.FirstOrDefaultAsync(u => u.UID == request.CalleeId);
        if (callee == null)
            return BadRequest(new { message = "error", details = "Invalid callee." });

        var callLog = new CallLog
        {
            CallerId = user.UID,
            CalleeId = callee.UID,
            CallType = request.CallType
        };
        _context.CallLogs.Add(callLog);
        await _context.SaveChangesAsync();

        return Ok(new { message = "success", details = "Call log added successfully." });
    }

    [HttpPost("endCall")]
    public async Task<IActionResult> EndCall([FromBody] AddEndCallRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "error", details = "Invalid request." });

        var user = await _authService.GetUserFromHeader(Request.Headers["Authorization"].ToString());
        if (user == null)
            return Unauthorized(new { message = "error", details = "Invalid or expired token" });

        if (request.CallerId != user.UID)
            return Unauthorized(new { message = "error", details = "Invalid caller." });

        var callLog = await _context.CallLogs.FirstOrDefaultAsync(c => c.CallerId == request.CallerId && c.CalleeId == request.CalleeId && c.EndTime == null);
        if (callLog == null)
            return BadRequest(new { message = "error", details = "Call log not found." });

        callLog.EndTime = DateTime.UtcNow;
        _context.CallLogs.Update(callLog);
        await _context.SaveChangesAsync();

        return Ok(new { message = "success", details = "Call log updated successfully." });
    }
}

public class AddStartCallRequest
{
    public int CallerId { get; set; }
    public int CalleeId { get; set; }
    public string? CallType { get; set; }
}

public class AddEndCallRequest
{
    public int CallerId { get; set; }
    public int CalleeId { get; set; }
}

