using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;


[Route("api/user")]
[ApiController]
public class UserController : ControllerBase   // All comment part is for db
{
    // private readonly ApplicationDbContext _context; // ✅ Inject database context
    private readonly IConfiguration _config;

    public UserController(IConfiguration config
        // ApplicationDbContext context for DB
        )
    {
        _config = config;

        // _context = context;
    }

    [HttpPost("updateUser")]
    public async Task<IActionResult> UpdateUser()
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Unauthorized(new { error = "Missing token" });
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var principal = ValidateJwtToken(token);
        if (principal == null)
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        // ✅ Extract user email from token
        var userEmail = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail))
        {
            return Unauthorized(new { error = "User email not found in token" });
        }

        // ✅ Read JSON body from request
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
        {
            return BadRequest(new { message = "Invalid input" });
        }

        Console.WriteLine($"Received update request from: {userEmail}");
        Console.WriteLine($"Request Body: {body}");

        return Ok(new
        {
            message = "User updated successfully",
            data = body 
            // data{user {email = userEmail, name = userName, gender = userGender, description = userDescription}}
        });
    }

    private ClaimsPrincipal? ValidateJwtToken(string token)
    {
        var key = Encoding.UTF8.GetBytes(_config["JwtSettings:Key"]);
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = _config["JwtSettings:Issuer"],
            ValidateAudience = true,
            ValidAudience = _config["JwtSettings:Audience"],
            ValidateLifetime = true
        };

        try
        {
            return tokenHandler.ValidateToken(token, validationParameters, out _);
        }
        catch
        {
            return null;
        }
    }

    [HttpPost("updateImage")]
    public async Task<IActionResult> UpdateImage(IFormFile file)
    {

        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Unauthorized(new { error = "Missing token" });
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var principal = ValidateJwtToken(token);
        if (principal == null)
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        var userEmail = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail))
        {
            return Unauthorized(new { error = "User email not found in token" });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded" });
        }

        try
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var imageUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";

            // ✅ If using a database, update the user’s profile with the new image URL
            // var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            // if (user == null)
            // {
            //     return NotFound(new { message = "User not found" });
            // }
            // user.ProfileImageUrl = imageUrl;
            // await _context.SaveChangesAsync();
            return Ok(new { message = "Image uploaded successfully", imageUrl = "https://img.freepik.com/free-photo/serious-young-african-man-standing-isolated_171337-9633.jpg" });

            // return Ok(new { message = "Image uploaded successfully", imageUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Image upload failed", details = ex.Message });
        }
    }
}

// ✅ Create a DTO (Data Transfer Object) for update request
// public class UpdateUserRequest
// {
//     public string Name { get; set; }
//     public string Gender { get; set; }
//     public string Description { get; set; }
// }