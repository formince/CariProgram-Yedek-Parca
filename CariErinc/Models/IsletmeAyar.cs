using System.ComponentModel.DataAnnotations;

namespace CariErinc.Models;

/// <summary>İşletme ayarları — anahtar/değer çifti, DB'de saklanır ve cache'lenir.</summary>
public class IsletmeAyar
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Anahtar { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Deger { get; set; } = string.Empty;
}
