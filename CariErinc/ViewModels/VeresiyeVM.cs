using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CariErinc.ViewModels;

public class VeresiyeVM
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Müşteri seçiniz")]
    [Display(Name = "Müşteri")]
    public int MusteriId { get; set; }

    [Required(ErrorMessage = "Tutar gereklidir")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Tutar 0'dan büyük olmalıdır")]
    [Display(Name = "Tutar")]
    public decimal Tutar { get; set; }

    [Display(Name = "Açıklama")]
    public string? Aciklama { get; set; }

    public CariErinc.Models.VeresiyeTipi Tip { get; set; } = CariErinc.Models.VeresiyeTipi.SatisBagli;

    public SelectList? MusteriListesi { get; set; }
}
