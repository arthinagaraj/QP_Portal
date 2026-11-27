using System.Text;
using DbUp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QpPortal.Api.Auth;
using QpPortal.Api.Data;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration.GetConnectionString("Db")
         ?? throw new InvalidOperationException("Connection string 'Db' is missing.");

var scriptsPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, "..", "..", "db", "scripts"));
EnsureDatabase.For.SqlDatabase(cs);
var upgrade = DeployChanges.To.SqlDatabase(cs)
    .WithScriptsFromFileSystem(scriptsPath)
    .LogToConsole()
    .Build()
    .PerformUpgrade();
if (!upgrade.Successful) throw upgrade.Error;

builder.Services.AddDbContext<AppDb>(o => o.UseSqlServer(cs));

var jwt = builder.Configuration.GetSection("Jwt");
var jwtKey = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key missing - run dotnet user-secrets set.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt["Issuer"],
        ValidateAudience = true, ValidAudience = jwt["Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true
    });

var authz = builder.Services.AddAuthorizationBuilder();
foreach (var p in Perms.All)
    authz.AddPolicy(p, policy => policy.RequireClaim("perm", p));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await DemoSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDb>());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();