using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using QpPortal.Api.Data;

namespace QpPortal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDb db, IConfiguration config) : ControllerBase
{
    public record LoginRequest(string LoginId, string Password);
    static readonly string[] DobFormats = ["ddMMyyyy", "dd-MM-yyyy", "dd/MM/yyyy", "dd.MM.yyyy", "yyyy-MM-dd"];
    public record SetterLoginRequest(string LoginId, string DateOfBirth);
    public record LoginResponse(string Token, string Name, string[] Roles, string[] Permissions);

    private IQueryable<User> UsersWithPerms() => db.Users
        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission);

        // Single login for everyone:
    //  - exam cell (CoE / DCoE / HoD): e-mail or mobile + password
    //  - QP setter: e-mail or mobile + date of birth (DDMMYYYY)
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        var id = (req.LoginId ?? "").Trim();
        var pwd = (req.Password ?? "").Trim();
        var user = await UsersWithPerms().FirstOrDefaultAsync(u => u.Email == id || u.Mobile == id);
        if (user is null || !user.IsActive) return Unauthorized("Invalid login ID or password.");

        bool ok;
        if (user.PasswordHash is not null)
            ok = BCrypt.Net.BCrypt.Verify(pwd, user.PasswordHash);
        else
            ok = user.DateOfBirth is not null
                 && DateOnly.TryParseExact(pwd, DobFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                 && d == user.DateOfBirth;

        if (!ok) return Unauthorized("Invalid login ID or password. QP setters: use your date of birth as DDMMYYYY.");
        if (user.RegistrationStatus == "Pending")
            return Unauthorized("Your registration is awaiting approval by the exam cell.");
        if (user.RegistrationStatus == "Rejected")
            return Unauthorized($"Your registration was not approved: {user.RejectionReason}");
        return Issue(user);
    }

    // Login 2: QP setter with mobile or e-mail + date of birth
    [HttpPost("setter-login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> SetterLogin(SetterLoginRequest req)
    {
        if (!DateOnly.TryParseExact(req.DateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dob))
            return BadRequest("Enter date of birth as YYYY-MM-DD.");

        var id = req.LoginId.Trim();
        var user = await UsersWithPerms().FirstOrDefaultAsync(u =>
            (u.Email == id || u.Mobile == id) && u.DateOfBirth == dob && u.IsActive);

        if (user is null || !user.UserRoles.Any(ur => ur.Role.Name == "Setter"))
            return Unauthorized("Login ID or date of birth is incorrect.");
        if (user.RegistrationStatus == "Pending")
            return Unauthorized("Your registration is awaiting approval by the exam cell.");
        if (user.RegistrationStatus == "Rejected")
            return Unauthorized($"Your registration was not approved: {user.RejectionReason}");
        return Issue(user);
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        name = User.Identity?.Name,
        roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value),
        permissions = User.FindAll("perm").Select(c => c.Value)
    });

    private ActionResult<LoginResponse> Issue(User user)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToArray();
        var perms = user.UserRoles.SelectMany(ur => ur.Role.RolePermissions)
                                  .Select(rp => rp.Permission.Key).Distinct().ToArray();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(perms.Select(p => new Claim("perm", p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = config["Jwt:Issuer"],
            Audience = config["Jwt:Audience"],
            Expires = DateTime.UtcNow.AddHours(10),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });

        return new LoginResponse(token, user.Name, roles, perms);
    }
}