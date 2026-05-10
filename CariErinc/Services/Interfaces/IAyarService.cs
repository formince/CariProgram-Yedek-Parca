namespace CariErinc.Services.Interfaces;

public interface IAyarService
{
    Task<string?> GetAsync(string anahtar);
    Task SetAsync(string anahtar, string deger);
    Task<Dictionary<string, string>> GetAllAsync();
    void InvalidateCache();

    /// <summary>İşletme ayarındaki geçerli KDV yüzdeleri; ayar yoksa varsayılan set.</summary>
    Task<IReadOnlyList<int>> GetKdvOranlariListeAsync(CancellationToken cancellationToken = default);
    Task<int> GetVarsayilanKdvOraniAsync();
}
