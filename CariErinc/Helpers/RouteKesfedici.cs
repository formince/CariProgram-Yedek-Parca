using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace CariErinc.Helpers;

public record ControllerActionBilgi(string Controller, string Action);

/// <summary>
/// Reflection ile projedeki tüm public Controller/Action çiftlerini tarar.
/// Uygulama başlarken bir kez çağrılır; sonuç bellek içinde cache'lenir.
/// </summary>
public static class RouteKesfedici
{
    private static List<ControllerActionBilgi>? _cache;

    public static IReadOnlyList<ControllerActionBilgi> Tara()
    {
        if (_cache is not null) return _cache;

        var assembly = Assembly.GetExecutingAssembly();

        _cache = assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(Controller).IsAssignableFrom(t))
            .SelectMany(t =>
            {
                var controllerAdi = t.Name.Replace("Controller", "");
                return t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(IsActionMethod)
                    .Select(m => new ControllerActionBilgi(controllerAdi, m.Name));
            })
            .DistinctBy(x => (x.Controller, x.Action))
            .OrderBy(x => x.Controller)
            .ThenBy(x => x.Action)
            .ToList();

        return _cache;
    }

    private static bool IsActionMethod(MethodInfo m)
    {
        if (m.IsSpecialName) return false;
        var rt = m.ReturnType;
        if (typeof(IActionResult).IsAssignableFrom(rt)) return true;
        if (rt.IsGenericType && rt.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var arg = rt.GetGenericArguments()[0];
            return typeof(IActionResult).IsAssignableFrom(arg);
        }
        return false;
    }
}
