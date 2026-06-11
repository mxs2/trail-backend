using Microsoft.EntityFrameworkCore;
using Trail.Api.Infrastructure.Data;

namespace Trail.Api.Tests.Helpers;

/// <summary>
/// Cria um <see cref="AppDbContext"/> isolado em memória para cada teste.
/// Cada chamada usa um nome de banco único, garantindo que os testes não
/// compartilhem estado entre si.
/// </summary>
public static class TestDb
{
    public static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"trail-tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;

        return new AppDbContext(options);
    }
}
