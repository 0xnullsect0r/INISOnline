using InisServer.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace INISServer.Tests;

/// <summary>
/// Hosts the real server in-process for integration tests, swapping PostgreSQL for a private
/// in-memory Sqlite database (one kept-open connection per factory so the schema persists for
/// the factory's lifetime). The production wiring — auth, sessions, WebSockets — is otherwise
/// unchanged.
/// </summary>
public class InisAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        _connection.Open();

        // The server refuses to boot outside Development on the dev signing key.
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789abcdef");

        builder.ConfigureServices(services =>
        {
            // Replace the Npgsql DbContext registration with shared in-memory Sqlite. Remove
            // every EF options descriptor (DbContextOptions + the options-configuration service)
            // so only one provider remains registered.
            var stale = services.Where(d =>
                d.ServiceType == typeof(AppDbContext) ||
                (d.ServiceType.FullName?.Contains("DbContextOptions") ?? false)).ToList();
            foreach (var d in stale) services.Remove(d);

            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
