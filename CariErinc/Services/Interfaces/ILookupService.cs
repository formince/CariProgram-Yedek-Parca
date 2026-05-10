using Microsoft.AspNetCore.Mvc.Rendering;

namespace CariErinc.Services.Interfaces;

public interface ILookupService
{
    Task<int> GetVarsayilanKdvAsync();
    Task<SelectList> GetKdvOranlariAsync(int currentKdv);
    Task<SelectList> GetTedarikcilerAsync(int? currentSelected = null);
    Task<SelectList> GetUrunKategorileriAsync(string? currentSelected = null);
    Task<SelectList> GetMusteriSelectListAsync(int? currentSelected = null, string emptyText = "-- Müşteri Seçin --");
    Task<SelectList> GetUrunSelectListAsync(int? currentSelected = null);
}
