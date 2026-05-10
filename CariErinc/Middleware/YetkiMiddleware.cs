using CariErinc.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace CariErinc.Middleware;

public class YetkiMiddleware
{
    private readonly RequestDelegate _next;

    public YetkiMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IYetkiCacheService yetkiCache)
    {
        // Giriş yapmamış → Authentication middleware halleder
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Admin bypass: sınırsız erişim
        if (context.User.HasClaim("is_admin", "true"))
        {
            await _next(context);
            return;
        }

        // Controller/Action bilgisini endpoint metadata'dan al
        var endpoint = context.GetEndpoint();
        if (endpoint is null)
        {
            await _next(context);
            return;
        }

        // [AllowAnonymous] attribute varsa geç
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context);
            return;
        }

        // Controller/Action adlarını al
        var actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (actionDescriptor is null)
        {
            await _next(context);
            return;
        }

        var controllerAdi = actionDescriptor.ControllerName;
        var actionAdi = actionDescriptor.ActionName;

        // Kullanıcının rol id'lerini claim'den oku
        var rolIdsClaim = context.User.FindFirst("rol_ids")?.Value;
        if (string.IsNullOrEmpty(rolIdsClaim))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Erişim reddedildi: Rol atanmamış.");
            return;
        }

        var rolIds = rolIdsClaim
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .Where(n => n > 0)
            .ToArray();

        var yetkiler = await yetkiCache.GetYetkilerAsync(rolIds);

        if (yetkiler.Contains((controllerAdi, actionAdi)))
        {
            await _next(context);
            return;
        }

        // Yetkisiz → 403 sayfasına yönlendir
        context.Response.Redirect("/Auth/Yetkisiz");
    }
}
