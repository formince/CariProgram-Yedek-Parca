using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using CariErinc.Services;

namespace CariErinc.Data;

public class TenantDbContextFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public TenantDbContextFactory(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    public AppDbContext CreateDbContext()
    {
        var connStr = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection yapılandırılmamış.");

        if (_httpContextAccessor.HttpContext?.Items["TenantInfo"] is TenantInfo tenantInfo
            && !string.IsNullOrWhiteSpace(tenantInfo.ConnectionString))
            connStr = tenantInfo.ConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connStr)
            .Options;

        return new AppDbContext(options);
    }
}
