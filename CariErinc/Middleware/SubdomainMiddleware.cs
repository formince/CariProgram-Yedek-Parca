using CariErinc.Data;
using CariErinc.Models;
using CariErinc.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace CariErinc.Middleware;

public class SubdomainMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan TenantCacheTtl = TimeSpan.FromMinutes(5);

    public SubdomainMiddleware(
        RequestDelegate next,
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration config)
    {
        var enabled = config.GetValue<bool>("MultiTenant:Enabled");
        if (!enabled)
        {
            await _next(context);
            return;
        }

        var host = context.Request.Host.Host;
        var baseDomain = config["MultiTenant:BaseDomain"] ?? "";

        if (!TryResolveSubdomain(host, baseDomain, out var subdomain)
            || subdomain.Equals("www", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Tenant bulunamadı.");
            return;
        }

        var cacheKey = "tenant_sub_" + subdomain.ToLowerInvariant();
        if (!_cache.TryGetValue(cacheKey, out TenantKayit? kayit))
        {
            using var scope = _scopeFactory.CreateScope();
            var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
            kayit = await adminDb.TenantKayitlar.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Subdomain == subdomain && t.AktifMi);

            if (kayit != null)
                _cache.Set(cacheKey, kayit, TenantCacheTtl);
        }

        if (kayit == null || string.IsNullOrWhiteSpace(kayit.ConnectionString))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Tenant bulunamadı.");
            return;
        }

        context.Items["TenantInfo"] = new TenantInfo
        {
            Id = kayit.Id,
            Subdomain = kayit.Subdomain,
            DukkanAdi = kayit.DukkanAdi,
            ConnectionString = kayit.ConnectionString
        };

        await _next(context);
    }

    private static bool TryResolveSubdomain(string host, string baseDomain, out string subdomain)
    {
        subdomain = "";

        if (string.IsNullOrWhiteSpace(host))
            return false;

        host = host.Trim();

        if (!string.IsNullOrWhiteSpace(baseDomain)
            && host.EndsWith(baseDomain, StringComparison.OrdinalIgnoreCase))
        {
            var prefix = host[..^baseDomain.Length].TrimEnd('.');
            if (string.IsNullOrEmpty(prefix))
                return false;
            subdomain = prefix;
            return true;
        }

        // Örn. tenant.localhost veya tek parça hostlar
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            subdomain = parts[0];
            return !string.IsNullOrEmpty(subdomain);
        }

        return false;
    }
}
