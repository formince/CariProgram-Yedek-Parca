using CariErinc.Models;
using CariErinc.ViewModels;
using CariErinc.Helpers;

namespace CariErinc.Services.Interfaces;

public interface IAlisService
{
    Task<AlisIndexVM> GetPagedListAsync(int page, int? tedarikciId, DateTime? baslangic, DateTime? bitis);
    Task<Alis?> GetByIdAsync(int id);
    Task<ServiceResult> SaveAsync(AlisVM vm);
    Task<ServiceResult> SilAsync(int id);
    Task<List<Alis>> GetVadeliAcikAlislarAsync(int? tedarikciId = null);
    Task<ServiceResult> OdemeYapAsync(int alisId, decimal tutar, string? aciklama);
}
