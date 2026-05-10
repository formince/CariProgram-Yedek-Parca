using Npgsql;

namespace CariErinc.Helpers;

public static class PostgresConnectionStringHelper
{
    /// <summary>
    /// postgresql:// URL veya Host=... connection string — Npgsql'in anladığı anahtar=değer biçimine çevirir.
    /// Placeholder / bozuk değerlerde false döner (ApplyRailwayDefaults patlamaz).
    /// </summary>
    public static bool TryNormalizeForNpgsql(string? connectionStringOrUrl, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(connectionStringOrUrl))
            return false;

        var s = connectionStringOrUrl.Trim().Trim('"').Trim('\uFEFF');
        if (s.Contains("{PGHOST}", StringComparison.Ordinal)
            || s.Contains("{PGDATABASE}", StringComparison.Ordinal)
            || s.Contains("{PGUSER}", StringComparison.Ordinal))
            return false;

        if (s.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = ToNpgsqlConnectionString(s);
            return !string.IsNullOrEmpty(normalized);
        }

        s = StripObsoleteNpgsqlPairKeys(s);

        try
        {
            normalized = ApplyRailwayDefaults(s);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Railway'de yalnızca veritabanı adı yazıldıysa (örn. myeo_admin) tam Npgsql string üretir.
    /// </summary>
    public static string? MergeDatabaseOntoNpgsqlBase(string? npgsqlBaseConnection, string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(npgsqlBaseConnection) || string.IsNullOrWhiteSpace(databaseName))
            return null;
        var name = databaseName.Trim().Trim('"');
        if (name.Contains(';', StringComparison.Ordinal) || name.Contains('=', StringComparison.Ordinal))
            return null;
        try
        {
            var b = new NpgsqlConnectionStringBuilder(npgsqlBaseConnection) { Database = name };
            return b.ConnectionString;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Sadece database adı gibi görünüyor mu (Host=... / URI değil).
    /// </summary>
    public static bool LooksLikeDatabaseNameOnly(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;
        var t = s.Trim().Trim('"');
        if (t.Length is < 1 or > 63)
            return false;
        if (t.Contains('=', StringComparison.Ordinal) || t.Contains(';', StringComparison.Ordinal))
            return false;
        if (t.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    /// <summary>
    /// Railway DATABASE_URL (postgresql://user:şifre@host:port/db) → Npgsql anahtar=değer.
    /// </summary>
    public static string? ToNpgsqlConnectionString(string? databaseUrl)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
            return null;

        var trimmed = databaseUrl.Trim();
        if (!trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            return ApplyRailwayDefaults(trimmed);

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return null;

        var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.None);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

        var dbFromPath = uri.AbsolutePath.Trim('/').Split('/')[0];
        var database = string.IsNullOrEmpty(dbFromPath) ? "postgres" : dbFromPath;

        var port = uri.Port > 0 ? uri.Port : 5432;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            SslMode = SslMode.Require,
            GssEncryptionMode = GssEncryptionMode.Disable
        };

        return builder.ConnectionString;
    }

    /// <summary>
    /// Npgsql 7+ artık desteklemez; DB'ye yapıştırılan eski string'ler builder'ı bozuyor.
    /// </summary>
    private static string StripObsoleteNpgsqlPairKeys(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var kept = parts.Where(p =>
        {
            var keyEnd = p.IndexOf('=');
            var key = keyEnd > 0 ? p[..keyEnd].Trim() : p;
            return !key.Equals("Trust Server Certificate", StringComparison.OrdinalIgnoreCase)
                   && !key.Equals("TrustServerCertificate", StringComparison.OrdinalIgnoreCase);
        });
        return string.Join(';', kept);
    }

    private static string ApplyRailwayDefaults(string connectionString)
    {
        var b = new NpgsqlConnectionStringBuilder(connectionString)
        {
            GssEncryptionMode = GssEncryptionMode.Disable
        };

        if (b.SslMode is SslMode.Disable or SslMode.Prefer)
            b.SslMode = SslMode.Require;

        return b.ConnectionString;
    }
}
