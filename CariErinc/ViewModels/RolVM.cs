using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CariErinc.ViewModels;

public class RolVM
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Rol adı zorunludur.")]
    [MaxLength(50)]
    [Display(Name = "Rol Adı")]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Açıklama")]
    public string? Aciklama { get; set; }

    [Display(Name = "Admin (tam yetki)")]
    public bool IsAdmin { get; set; }
}

public class RolYetkiDuzenleVM
{
    public int RolId { get; set; }
    public string RolAdi { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }

    /// <summary>Mevcut atanmış yetkiler (Controller+Action çiftleri).</summary>
    public HashSet<string> AtanmisYetkiler { get; set; } = new();

    /// <summary>Reflection ile keşfedilen tüm Controller/Action grupları.</summary>
    public List<ControllerGrubu> ControllerGruplari { get; set; } = new();

    /// <summary>Bir controller/action'ın mevcut sidebar ayarı.</summary>
    public Dictionary<string, RolYetkiSatirVM> SidebarAyarlari { get; set; } = new();
}

public class ControllerGrubu
{
    public string Controller { get; set; } = string.Empty;
    public List<string> Actions { get; set; } = new();
}

public class RolYetkiSatirVM
{
    public string SidebarGrubu { get; set; } = string.Empty;
    public string SidebarGoruntuAdi { get; set; } = string.Empty;
    public int SidebarSira { get; set; }
}
