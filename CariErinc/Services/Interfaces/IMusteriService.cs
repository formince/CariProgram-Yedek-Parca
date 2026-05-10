using CariErinc.Models;
using CariErinc.ViewModels;
using CariErinc.Helpers;

namespace CariErinc.Services.Interfaces;

public interface IMusteriService
{
    Task<MusteriIndexVM> GetPagedListAsync(int page = 1, string? arama = null);
    Task<List<Musteri>> GetAllAsync(string? arama = null);
    Task<Musteri?> GetByIdAsync(int id);
    Task<MusteriDetayVM?> GetDetayVMAsync(int id);
    Task<ServiceResult> SaveAsync(MusteriVM vm);
    Task<ServiceResult> SilAsync(int id);
    Task<List<Musteri>> SearchAsync(string query, int limit);
}
