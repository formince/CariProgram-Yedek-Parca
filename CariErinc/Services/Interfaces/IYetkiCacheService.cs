using CariErinc.Models;
using Microsoft.Extensions.Caching.Memory;

namespace CariErinc.Services.Interfaces;

public interface IYetkiCacheService
{
    /// <summary>Kullanıcının erişebildiği Controller+Action çiftlerini döner (cache'li).</summary>
    Task<HashSet<(string Controller, string Action)>> GetYetkilerAsync(IEnumerable<int> rolIds);

    /// <summary>Kullanıcının sidebar'da göreceği linkleri döner (SidebarGoruntuAdi dolu olanlar).</summary>
    Task<List<RolYetki>> GetSidebarLinksAsync(IEnumerable<int> rolIds);

    /// <summary>Belirli roller değiştiğinde cache'i temizle.</summary>
    void InvalidateRol(int rolId);

    void InvalidateAll();
}
