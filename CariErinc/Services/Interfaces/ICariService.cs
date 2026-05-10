using CariErinc.Helpers;
using CariErinc.ViewModels;

namespace CariErinc.Services.Interfaces;

public interface ICariService
{
    Task<CariIndexVM> GetIndexVMAsync(string? arama);
    Task<CariDetayVM?> GetDetayVMAsync(int cariId);
    Task<CariEkstreVM?> GetEkstreVMAsync(int cariId, int page = 1, DateTime? baslangic = null, DateTime? bitis = null);
    Task<CariFormVM?> GetFormVMAsync(int id);
    Task<ServiceResult> SaveAsync(CariFormVM vm);
    Task<ServiceResult> SilAsync(int id);
}
