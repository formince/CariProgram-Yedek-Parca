using Npgsql;

namespace CariErinc.Helpers;

public static class PostgresConnectionStringHelper
{
    /// <summary>
    /// postgresql:// URL veya Host=... connection string — Npgsql'in anladığı anahtar=değer biçimine çevirir.
    /// </summary>
    public static string? NormalizeForNpgsql(string? connectionStringOrUrl)
    {
        if (string.IsNullOrWhiteSpace(connectionStringOrUrl))
            return null;

        var s = connectionStringOrUrl.Trim().Trim('"');
        if (s.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            return ToNpgsqlConnectionString(s);

        return ApplyRailwayDefaults(s);
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
