using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

[Route("api/auth")]
[ApiController]
public class UserController : ControllerBase
{
    [HttpGet("user")]
    [Authorize]
    public IActionResult GetUser()
    {
        if (!User.Identity.IsAuthenticated)
        {
            return Unauthorized(new { message = "User is not authenticated" });
        }

        var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            return Unauthorized(new { message = "Email claim not found" });
        }

        return Ok(new { email });
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
