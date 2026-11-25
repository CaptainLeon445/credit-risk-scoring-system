using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CreditRiskScoring.Api.Infrastructure.Security;

public class JwtService(IConfiguration config)
{
    private readonly string _secret    = config["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
    private readonly string _issuer    = config["Jwt:Issuer"] ?? "CreditRiskApi";
    private readonly string _audience  = config["Jwt:Audience"] ?? "CreditRiskClient";
    private readonly int    _expiryMin = int.Parse(config["Jwt:ExpiryMinutes"] ?? "1440");

    public string GenerateToken(string userId, string username, string role)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim("userId", userId),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(_expiryMin),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public int ExpirySeconds => _expiryMin * 60;
}
