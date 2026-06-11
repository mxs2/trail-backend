using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Trail.Api.Application.Services;
using Trail.Api.Configuration;
using Trail.Api.Domain.Entities;
using Trail.Api.Domain.Enums;
using Xunit;

namespace Trail.Api.Tests.Services;

public class TokenServiceTests
{
    private static TokenService BuildService() =>
        new(Options.Create(new JwtOptions
        {
            Secret = "trail-unit-tests-super-secret-key-0123456789abcd",
            Issuer = "trail-tests",
            Audience = "trail-tests"
        }));

    // Teste B5 — O JWT gerado deve conter as claims de identidade e o papel do
    // usuário, pois toda a autorização por role depende delas.
    [Fact]
    public void GenerateToken_IncludesSubjectEmailAndRoleClaims()
    {
        var service = BuildService();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Mentor Teste",
            Email = "mentor@trail.com",
            Role = UserRole.Mentor
        };

        var token = service.GenerateToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("mentor@trail.com", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("Mentor", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal("trail-tests", jwt.Issuer);
    }

    // Teste B6 — O token deve ter expiração futura (token recém-emitido é válido).
    [Fact]
    public void GenerateToken_SetsFutureExpiration()
    {
        var service = BuildService();
        var user = new User { Id = Guid.NewGuid(), Name = "X", Email = "x@trail.com", Role = UserRole.Student };

        var token = service.GenerateToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
    }
}
