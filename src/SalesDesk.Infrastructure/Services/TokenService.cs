using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Users;

namespace SalesDesk.Infrastructure.Services;

/// <summary>
/// Issues the signed JWT clients attach as a Bearer token on every subsequent
/// request. Claims carried here are exactly what <c>CurrentUserService</c> (API
/// layer) reads back to answer "who is making this call, in which workspace, with
/// which role" without a database round-trip per request.
/// </summary>
public sealed class TokenService(IConfiguration configuration) : ITokenService
{
    public AccessToken IssueToken(User user)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var expiryMinutes = int.TryParse(jwtSection["ExpiryMinutes"], out var minutes) ? minutes : 60 * 12;

        return BuildToken(user, TimeSpan.FromMinutes(expiryMinutes));
    }

    public AccessToken IssueImpersonationToken(User target, TimeSpan lifetime) => BuildToken(target, lifetime);

    private AccessToken BuildToken(User user, TimeSpan lifetime)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var secret = jwtSection["Secret"]
            ?? throw new InvalidOperationException(
                "Jwt:Secret is not configured. Set it in appsettings or the Jwt__Secret environment variable.");
        var issuer = jwtSection["Issuer"] ?? "SalesDesk";
        var audience = jwtSection["Audience"] ?? "SalesDesk";

        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("workspace_id", user.WorkspaceId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: signingCredentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
