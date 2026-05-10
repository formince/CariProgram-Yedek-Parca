using System.ComponentModel.DataAnnotations;
using CariErinc.Models;

namespace CariErinc.ViewModels;

public class ParcaKoduVM
{
    public int Id { get; set; }
    public int UrunId { get; set; }

    public ParcaKoduTipi KodTipi { get; set; }

    [MaxLength(100)]
    public string Kod { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Aciklama { get; set; }
}
