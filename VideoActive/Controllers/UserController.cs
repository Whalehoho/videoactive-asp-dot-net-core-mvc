using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using VideoActive.Models;
using System.Text.Json;

[Route("api/user")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ApplicationDbContext _context;
    private readonly AuthService _authService;

    public UserController(IConfiguration config, ApplicationDbContext context, AuthService authService)
    {
        _config = config;
        _context = context;
        _authService = authService;
    }
    [HttpPost("updateUser")]
    public async Task<IActionResult> UpdateUser([FromBody] UpdateUserRequest request)
    {
        var user = await _authService.GetUserFromToken(Request.Headers["Authorization"].ToString());
        if (user == null)
            return Unauthorized(new { message = "error", details= "Invalid or expired token" });

        if (request == null)
            return BadRequest(new { message = "error", details ="Invalid input" });

        if (!string.IsNullOrWhiteSpace(request.Username))
            user.Username = request.Username;

        if (request.Gender.HasValue)
            user.Gender = request.Gender.Value;

        if (!string.IsNullOrWhiteSpace(request.Description))
            user.Description = request.Description;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "success",
            details = "User updated successfully",
            user = new
            {
                user.UID,
                user.Username,
                user.Email,
                user.Gender,
                user.Description
            }
        });
    }

    [HttpPost("updateImage")]
    public async Task<IActionResult> UpdateImage(IFormFile file)
    {
        var user = await _authService.GetUserFromToken(Request.Headers["Authorization"].ToString());
        if (user == null)
            return Unauthorized(new { message = "error", details="Invalid or expired token" });

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "error", details="No file uploaded" });

        try
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var imageUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";
            user.ProfilePic = imageUrl;
            await _context.SaveChangesAsync();

            return Ok(new { message = "success", details="Image uploaded successfully", imageUrl = imageUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Image upload failed", details = ex.Message });
        }
    }
}

public class UpdateUserRequest
{
    public string? Username { get; set; }
    public bool? Gender { get; set; }
    public string? Description { get; set; }
}
