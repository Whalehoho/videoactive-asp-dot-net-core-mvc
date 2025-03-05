using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using VideoActive.Models;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;


[Route("api/user")]
[ApiController]
public class UserController : ControllerBase   // All comment part is for db
{
    // private readonly ApplicationDbContext _context; // ✅ Inject database context
    private readonly IConfiguration _config;
    private readonly ApplicationDbContext _context;

    public UserController(IConfiguration config, ApplicationDbContext context
        // ApplicationDbContext context for DB
        )
    {
        _config = config;
        _context = context;

        // _context = context;
    }


    [HttpGet("getUser")]
    [Authorize] // Ensures only authenticated users can access this endpoint
    public async Task<IActionResult> GetUserProfile()
    {
        // Retrieve email from JWT token
        var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        
        if (string.IsNullOrEmpty(email))
            return Unauthorized(new { message = "User is not authenticated" });

        // Fetch user from database
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        
        if (user == null)
            return NotFound(new { message = "User not found" });

        // Return user details
        return Ok(new
        {
            user.UID,
            user.Username,
            user.Email,
            user.ProfilePic,
            user.Status,
            user.Description,
            user.Gender,
            user.CreatedAt
        });
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
      

        // ✅ Parse the request body into a dictionary
        var requestData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(body);

        if (requestData == null)
        {
            return BadRequest(new { message = "Invalid JSON format" });
        }

        // ✅ Find the user in the database
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        //Username
        if (requestData.ContainsKey("username"))
        {
            if (requestData["username"] is string newUsername && !string.IsNullOrWhiteSpace(newUsername))
            {
                user.Username = newUsername;
            }
            else
            {
                return BadRequest(new { message = "Invalid username value" });
            }
        }
        
        //Gender
        if (requestData.ContainsKey("gender"))
        {
            try
            {
                if (requestData["gender"] is JsonElement jsonElement)
                {
                    user.Gender = jsonElement.GetBoolean();  // ✅ Correctly extracts bool value
                }
                else if (requestData["gender"] is bool genderValue)
                {
                    user.Gender = genderValue;
                }
                else
                {
                    return BadRequest(new { message = "Invalid gender value, must be true or false" });
                }
            }
            catch
            {
                return BadRequest(new { message = "Invalid gender value, must be true or false" });
            }
        }

        
        //Description
        if (requestData.ContainsKey("description"))
        {
            if (requestData["description"] is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
            {
                user.Description = jsonElement.GetString();  // ✅ Extracts description safely
            }
            else if (requestData["description"] is string newDescription)
            {
                user.Description = newDescription;
            }
            else
            {
                return BadRequest(new { message = "Invalid description value" });
            }
        }


        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "User updated successfully",
            user = new
            {
                user.UID,
                user.Username,
                user.Email,
                Gender = user.Gender.HasValue ? (user.Gender.Value ? "Male" : "Female") : "Not Specified",
                user.Description
            }
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
 public class UpdateUserRequest
 {
     public string? Username { get; set; }
     public bool? Gender { get; set; }
     public string? Description { get; set; }
 }