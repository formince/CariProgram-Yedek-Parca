using CariErinc.Models;
using CariErinc.ViewModels;
using CariErinc.Helpers;

namespace CariErinc.Services.Interfaces;

public interface ITedarikciService
{
    Task<List<Tedarikci>> GetAllAsync(string? arama);
    Task<Tedarikci?> GetByIdAsync(int id);
    Task<TedarikciDetayVM?> GetDetayVMAsync(int id);
    Task<ServiceResult> SaveAsync(TedarikciVM vm);
    Task<ServiceResult> SilAsync(int id);
}
