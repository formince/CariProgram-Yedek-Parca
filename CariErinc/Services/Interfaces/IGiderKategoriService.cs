using CariErinc.Models;
using CariErinc.Helpers;

namespace CariErinc.Services.Interfaces;

public interface IGiderKategoriService
{
    Task<List<GiderKategori>> GetAllAsync(KasaHareketTipi? tip = null);
    Task<GiderKategori?> GetByIdAsync(int id);
    Task<ServiceResult> SaveAsync(GiderKategori kategori);
    Task<ServiceResult> SilAsync(int id);
}
