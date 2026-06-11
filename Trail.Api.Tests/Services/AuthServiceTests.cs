using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Trail.Api.Application.Services;
using Trail.Api.Configuration;
using Trail.Api.Domain.Entities;
using Trail.Api.Domain.Enums;
using Trail.Api.DTOs.Auth;
using Trail.Api.Infrastructure.Data;
using Trail.Api.Tests.Helpers;
using Xunit;

namespace Trail.Api.Tests.Services;

public class AuthServiceTests
{
    private const string FakeToken = "fake.jwt.token";

    private static AuthService BuildService(AppDbContext db, out Mock<ITokenService> tokenService)
    {
        tokenService = new Mock<ITokenService>();
        tokenService.Setup(t => t.GenerateToken(It.IsAny<User>())).Returns(FakeToken);

        var refreshOptions = Options.Create(new RefreshOptions { ExpirationDays = 14 });
        return new AuthService(db, tokenService.Object, refreshOptions);
    }

    // Teste B1 — Registrar com email já existente deve falhar (retornar null),
    // protegendo contra contas duplicadas.
    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ReturnsNull()
    {
        using var db = TestDb.NewContext();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Existente",
            Email = "dup@trail.com",
            PasswordHash = "x",
            Role = UserRole.Student
        });
        await db.SaveChangesAsync();

        var service = BuildService(db, out _);
        var request = new RegisterRequest("Novo", "dup@trail.com", "Senha@123", UserRole.Student);

        var result = await service.RegisterAsync(request);

        Assert.Null(result);
        Assert.Equal(1, await db.Users.CountAsync()); // nenhum usuário novo criado
    }

    // Teste B2 — Registro válido cria usuário + settings + refresh token e
    // retorna o LoginResponse com o papel e nome corretos.
    [Fact]
    public async Task RegisterAsync_WithNewEmail_PersistsUserAndReturnsResponse()
    {
        using var db = TestDb.NewContext();
        var service = BuildService(db, out _);
        var request = new RegisterRequest("Ana Dev", "ana@trail.com", "Senha@123", UserRole.Mentor);

        var result = await service.RegisterAsync(request);

        Assert.NotNull(result);
        Assert.Equal(FakeToken, result!.Token);
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Equal("Mentor", result.Role);
        Assert.Equal("Ana Dev", result.Name);

        var saved = await db.Users.Include(u => u.Settings).SingleAsync(u => u.Email == "ana@trail.com");
        Assert.NotNull(saved.Settings);                 // settings criadas junto
        Assert.NotEqual("Senha@123", saved.PasswordHash); // senha é armazenada com hash
        Assert.Equal(1, await db.RefreshTokens.CountAsync());
    }

    // Teste B3 — Login com senha errada nunca retorna token.
    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsNull()
    {
        using var db = TestDb.NewContext();
        SeedUserWithPassword(db, "user@trail.com", "SenhaCorreta@1", UserRole.Student);

        var service = BuildService(db, out _);
        var result = await service.LoginAsync(new LoginRequest("user@trail.com", "SenhaErrada@9"));

        Assert.Null(result);
    }

    // Teste B4 — Login com credenciais válidas retorna token e papel corretos.
    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsToken()
    {
        using var db = TestDb.NewContext();
        SeedUserWithPassword(db, "valid@trail.com", "SenhaCorreta@1", UserRole.Manager);

        var service = BuildService(db, out var tokenService);
        var result = await service.LoginAsync(new LoginRequest("valid@trail.com", "SenhaCorreta@1"));

        Assert.NotNull(result);
        Assert.Equal(FakeToken, result!.Token);
        Assert.Equal("Manager", result.Role);
        tokenService.Verify(t => t.GenerateToken(It.IsAny<User>()), Times.Once);
    }

    private static void SeedUserWithPassword(AppDbContext db, string email, string password, UserRole role)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Seed",
            Email = email,
            Role = role
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        db.Users.Add(user);
        db.SaveChanges();
    }
}
