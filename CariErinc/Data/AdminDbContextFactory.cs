using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CariErinc.Data;

public class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var basePath = Directory.GetCurrentDirectory();
        var cfg = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = cfg.GetConnectionString("AdminConnection")
            ?? cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("AdminConnection veya DefaultConnection gerekli.");

        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var opts = new DbContextOptionsBuilder<AdminDbContext>()
            .UseNpgsql(cs)
            .Options;

        return new AdminDbContext(opts);
    }
}
