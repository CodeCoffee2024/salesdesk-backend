using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SalesDesk.Api.Authorization;
using SalesDesk.Api.ErrorHandling;
using SalesDesk.Api.Services;
using SalesDesk.Application;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Users;
using SalesDesk.Infrastructure;
using SalesDesk.Infrastructure.Persistence;
using SalesDesk.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// TASK-019: the frontend now lives in its own repository/origin, so the API must
// opt browsers into cross-origin requests explicitly. Origins come from config
// (appsettings' Cors:AllowedOrigins, or the Cors__AllowedOrigins env var in
// deployment) rather than being hardcoded, since dev/staging/prod each talk to a
// different frontend origin.
const string CorsPolicyName = "ConfiguredOrigins";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwtSection["Secret"]
    ?? throw new InvalidOperationException(
        "Jwt:Secret is not configured. Set it in appsettings or the Jwt__Secret environment variable.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"] ?? "SalesDesk",
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"] ?? "SalesDesk",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
// TASK-016: role-based policies. SalesManager gets create/edit but not delete;
// Viewer gets neither (falls back to the base [Authorize] on GET actions).
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.CanManage, policy => policy.RequireRole(
        nameof(Role.SystemAdmin), nameof(Role.WorkspaceAdmin), nameof(Role.SalesManager)));

    options.AddPolicy(Policies.CanDelete, policy => policy.RequireRole(
        nameof(Role.SystemAdmin), nameof(Role.WorkspaceAdmin)));
});

// Every unhandled exception is turned into a standardized ProblemDetails response
// by GlobalExceptionHandler — see SalesDesk.Api.ErrorHandling.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// TASK-020: apply pending EF Core migrations on every real boot (dev and deployed
// environments alike) so a Railway/Render release doesn't need a separate manual
// migration step. Seeding stays dev-only — a deployed database must not be reset
// to demo data on every restart. "Testing" is the one exception: it's the sentinel
// environment WebApplicationFactory-based tests boot under specifically so a
// config/DI/routing smoke test doesn't need a live, reachable Postgres just to
// build the host — see SalesDeskApiFactory / HealthControllerTests.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<SalesDeskDbContext>();
    await dbContext.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await SalesDeskDbContextSeeder.SeedAsync(dbContext, passwordHasher);
    }
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseCors(CorsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

// AC4: every dashboard-facing API endpoint requires authentication by default.
// Endpoints that must stay open (health check, register/login/forgot-password) opt
// out individually via [AllowAnonymous].
app.MapControllers().RequireAuthorization();

app.Run();

// Exposes the implicit top-level Program type to WebApplicationFactory in the test projects.
public partial class Program { }
