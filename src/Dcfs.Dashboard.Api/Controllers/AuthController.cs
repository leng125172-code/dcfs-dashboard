using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dcfs.Dashboard.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string returnUrl = "/")
    {
        var safeReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        return Challenge(
            new AuthenticationProperties { RedirectUri = safeReturnUrl },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            name = User.Identity?.Name,
            claims = User.Claims.Select(claim => new { claim.Type, claim.Value }),
        });
    }
}
