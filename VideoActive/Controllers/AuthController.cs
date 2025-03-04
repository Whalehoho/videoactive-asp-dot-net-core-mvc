using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet("google-login")] // login from google
    public IActionResult GoogleLogin()
    {
        var redirectUrl = Url.Action(nameof(GoogleResponse), "Auth");
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google-response")] // redirect back to front end
    public async Task<IActionResult> GoogleResponse()
    {
        var authenticateResult = await HttpContext.AuthenticateAsync();
        if (!authenticateResult.Succeeded)
            return BadRequest("Error during authentication");

        var claims = authenticateResult.Principal.Identities.FirstOrDefault()?.Claims;
        var userInfo = new
        {
            Name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value,
            Email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
        };

        if (userInfo.Email == null)
            return BadRequest("Unable to retrieve user email");

        // Generate JWT Token
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
    [HttpGet("validate-token")] // validate the token and get User info and if principal is yes retrieve user info from db
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
            email = principal.FindFirst(ClaimTypes.Email)?.Value,
            name = principal.FindFirst(ClaimTypes.Name)?.Value
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
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // Remove authentication cookies
        Response.Cookies.Delete(".AspNetCore.Cookies");
        Response.Cookies.Delete("AuthToken");

        return Ok(new { message = "Logged out successfully" });
    }

}
