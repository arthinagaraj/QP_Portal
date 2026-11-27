using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QpPortal.Api.Auth;
using QpPortal.Api.Data;

namespace QpPortal.Api.Controllers;

[ApiController]
[Route("api/instructions")]
public class InstructionsController(AppDb db) : ControllerBase
{
    public record InstructionsDto(string Html);

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Get() =>
        Ok(new { html = (await db.Settings.FindAsync("CommonInstructions"))?.Value ?? "" });

    [HttpPut]
    [Authorize(Policy = Perms.InstructionsManage)]
    public async Task<IActionResult> Put(InstructionsDto dto)
    {
        var s = await db.Settings.FindAsync("CommonInstructions");
        if (s is null) db.Settings.Add(new Setting { Key = "CommonInstructions", Value = dto.Html });
        else s.Value = dto.Html;
        await db.SaveChangesAsync();
        return Ok();
    }
}