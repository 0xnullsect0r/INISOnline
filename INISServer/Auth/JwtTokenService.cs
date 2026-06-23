using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace InisServer.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "inis";
    public string Audience { get; set; } = "inis";
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 30;
}

/// <summary>Issues short-lived access tokens and opaque, hashed refresh tokens.</summary>
public sealed class JwtTokenService(JwtOptions options)
{
    public string CreateAccessToken(Guid userId, string username)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(options.AccessTokenMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Returns (rawToken, sha256Hash). Only the hash is stored server-side.</summary>
    public (string Raw, string Hash) CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        return (raw, Hash(raw));
    }

    public static string Hash(string raw)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
