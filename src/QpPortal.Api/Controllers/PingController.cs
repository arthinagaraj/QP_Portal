using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QpPortal.Api.Auth;

namespace QpPortal.Api.Controllers;

[ApiController]
[Route("api/ping")]
public class PingController : ControllerBase
{
    [HttpGet("template-manage")]
    [Authorize(Policy = Perms.TemplateManage)]
    public IActionResult TemplateManage() => Ok($"Hello {User.Identity?.Name}, you can manage templates.");
}