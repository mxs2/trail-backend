using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trail.Api.Infrastructure.Data;

namespace Trail.Api.Tests.Helpers;

/// <summary>
/// Sobe a API real em memória para testes de integração, mas troca o provedor
/// SQL Server por um banco InMemory para que nenhuma infraestrutura externa
/// seja necessária. O <c>DbSeeder</c> do Program.cs roda normalmente sobre o
/// banco InMemory, populando os usuários semente (manager/mentor/student).
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string SeededStudentEmail = "student@trail.com";
    public const string SeededMentorEmail = "mentor@trail.com";
    public const string SeededManagerEmail = "manager@trail.com";
    public const string SeedPassword = "Senha@123";

    public CustomWebApplicationFactory()
    {
        // As configurações abaixo são lidas durante Program.Main (em AddDatabase e
        // AddJwtAuthentication), antes de o host ser construído. Por isso são
        // injetadas como variáveis de ambiente — que WebApplication.CreateBuilder
        // lê por padrão — e não via ConfigureAppConfiguration (que chega tarde demais).
        // A connection string só precisa ser não-nula: o DbContext é trocado por
        // InMemory em ConfigureTestServices.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
            "Server=(test);Database=trail-tests;Trusted_Connection=True;");
        // Segredo com tamanho suficiente para HMAC-SHA256 (>= 256 bits).
        Environment.SetEnvironmentVariable("Jwt__Secret", "trail-integration-tests-super-secret-key-0123456789");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "trail-tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "trail-tests");
        Environment.SetEnvironmentVariable("Refresh__ExpirationDays", "14");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove o DbContext configurado com SQL Server, suas options e a
            // configuração de options (IDbContextOptionsConfiguration<AppDbContext>
            // no EF Core 10). Sem remover esta última, o callback UseSqlServer
            // continua ativo e conflita com o UseInMemoryDatabase abaixo.
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(AppDbContext) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericArguments().Contains(typeof(AppDbContext))))
                .ToList();

            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            // Banco InMemory compartilhado entre as requisições deste factory.
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("trail-integration-tests"));
        });
    }
}
