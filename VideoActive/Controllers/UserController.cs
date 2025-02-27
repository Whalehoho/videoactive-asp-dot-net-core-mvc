using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

[Route("api/user")]
[ApiController]
public class UserController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IActionResult GetUser()
    {
        var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        return Ok(new { email });
    }
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("AuthToken");
        return Ok(new { message = "Logged out successfully" });
    }
}
