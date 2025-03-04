using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VideoActive.Models;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ApplicationDbContext _context;

    public AuthController(IConfiguration config, ApplicationDbContext context)
    {
        _config = config;
        _context = context;
    }

    [HttpGet("google-login")]
    public IActionResult GoogleLogin()
    {
        var redirectUrl = Url.Action(nameof(GoogleResponse), "Auth");
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google-response")]
    public async Task<IActionResult> GoogleResponse()
    {
        var authenticateResult = await HttpContext.AuthenticateAsync();
        if (!authenticateResult.Succeeded)
            return BadRequest("Error during authentication");

        var claims = authenticateResult.Principal.Identities.FirstOrDefault()?.Claims;
        var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var username = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existingUser == null)
        {
            // Create a new user in the database
            var newUser = new User
            {
                Username = username,
                Email = email,
                Status = UserStatus.Online // Mark them as online upon login
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
        }

        var userInfo = new
        {
            Name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value,
            Email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
        };

        if (userInfo.Email == null)
            return BadRequest("Unable to retrieve user email");

        //Generate JWT Token
        var token = GenerateJwtToken(userInfo.Email);

        // Set HTTP-Only Cookie
        Response.Cookies.Append("AuthToken", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // Set to false for local dev without HTTPS
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddHours(1)
        });

        return Redirect("http://localhost:3000/home");
    }

    private string GenerateJwtToken(string email)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JwtSettings:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["JwtSettings:Issuer"],
            audience: _config["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    [HttpGet("validate-token")]
    public IActionResult ValidateToken()
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

        var user = new
        {
            Name = principal.FindFirst(ClaimTypes.Email)?.Value
        };

        return Ok(new { user });
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
        try{
            return tokenHandler.ValidateToken(token, validationParameters, out _);}
        catch
        {return null;}
    }

    [HttpGet("check-session")]
    public IActionResult CheckSession()
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
            return Unauthorized(new { error = "Invalid or expired token" });
        }

        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        return Ok(new { isAuthenticated = true, email });
    }

}
