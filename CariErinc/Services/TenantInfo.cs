namespace CariErinc.Services;

public class TenantInfo
{
    public int Id { get; set; }
    public string Subdomain { get; set; } = string.Empty;
    public string DukkanAdi { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}
