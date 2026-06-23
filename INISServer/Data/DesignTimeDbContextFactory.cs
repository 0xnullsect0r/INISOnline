using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InisServer.Data;

/// <summary>
/// Lets EF Core tooling (<c>dotnet ef migrations …</c>) build an <see cref="AppDbContext"/>
/// without running the web host (which applies migrations against a live database at startup).
/// The connection string here is only used to select the provider for scaffolding — no
/// database connection is opened when generating migrations.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=inis;Username=inis;Password=inis")
            .Options;
        return new AppDbContext(options);
    }
}
