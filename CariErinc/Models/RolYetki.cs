using System.ComponentModel.DataAnnotations;

namespace CariErinc.Models;

/// <summary>
/// Bir rolün erişebildiği Controller/Action çifti.
/// SidebarGoruntuAdi dolu ise menüde de görünür; null ise sadece erişim izni (silme endpoint'leri vb.).
/// </summary>
public class RolYetki
{
    public int Id { get; set; }

    public int RolId { get; set; }
    public Rol Rol { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string ControllerAdi { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ActionAdi { get; set; } = string.Empty;

    /// <summary>null ise sidebar'da gösterilmez (sadece erişim izni).</summary>
    [MaxLength(60)]
    public string? SidebarGrubu { get; set; }

    [MaxLength(80)]
    public string? SidebarGoruntuAdi { get; set; }

    public int SidebarSira { get; set; }
}
