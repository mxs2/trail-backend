using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Trail.Api.DTOs.Auth;
using Trail.Api.DTOs.Submissions;
using Trail.Api.Tests.Helpers;
using Xunit;

namespace Trail.Api.Tests.Integration;

public class EndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EndpointsIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

    // Teste B17 — O endpoint público de health responde 200 OK, essencial para
    // health checks de deploy.
    [Fact]
    public async Task Health_Get_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Teste B18 — GET /trails sem token deve retornar 401 (pipeline de
    // autenticação está ativo e protege o endpoint).
    [Fact]
    public async Task Trails_Get_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/trails");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Teste B19 — Login com credenciais semente válidas retorna 200 e um token.
    [Fact]
    public async Task Login_WithSeededStudentCredentials_ReturnsToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(CustomWebApplicationFactory.SeededStudentEmail, CustomWebApplicationFactory.SeedPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("Student", body.Role);
    }

    // Teste B20 — Login com senha errada retorna 401.
    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(CustomWebApplicationFactory.SeededStudentEmail, "senha-errada"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Teste B21 — RBAC: POST /submissions é restrito ao papel Student. Um mentor
    // autenticado recebe 403 Forbidden.
    [Fact]
    public async Task PostSubmission_AsMentor_ReturnsForbidden()
    {
        var client = _factory.CreateClient();

        var token = await LoginAndGetTokenAsync(client,
            CustomWebApplicationFactory.SeededMentorEmail, CustomWebApplicationFactory.SeedPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/submissions",
            new CreateSubmissionRequest(Guid.NewGuid(), "https://github.com/aluno/repo"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }
}
