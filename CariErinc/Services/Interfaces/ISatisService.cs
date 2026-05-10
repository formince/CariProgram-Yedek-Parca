using CariErinc.Models;
using CariErinc.ViewModels;
using CariErinc.Helpers;

namespace CariErinc.Services.Interfaces;

public interface ISatisService
{
    Task<List<Satis>> GetAllAsync(int? musteriId, OdemeTipi? tip, DateTime? baslangic, DateTime? bitis, bool dahilIptaller = false);
    Task<SatisIndexVM> GetPagedListAsync(int page, int? musteriId, OdemeTipi? tip, DateTime? baslangic, DateTime? bitis, bool dahilIptaller = false);
    Task<Satis?> GetByIdAsync(int id);
    Task<ServiceResult> SaveAsync(SatisVM vm);
    Task<ServiceResult> SilAsync(int id, string? neden = null);
    Task<ServiceResult> KismiIadeAsync(SatisIadeVM vm);
    Task<ServiceResult<int>> TaslakKaydetAsync(SatisVM vm);
    Task<List<Satis>> GetTaslaklarAsync();
    Task<SatisVM?> TaslagiYukleAsync(int taslakId);
    Task<ServiceResult> TaslakSilAsync(int taslakId);
    Task<Satis?> GetForEditAsync(int id);
}
