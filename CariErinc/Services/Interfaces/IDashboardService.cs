using CariErinc.ViewModels;

namespace CariErinc.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardVM> GetDashboardVerileriAsync();
}
