using System.Globalization;
using System.Linq;
using CariErinc.Data;
using CariErinc.Helpers;
using CariErinc.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// DATABASE CONFIGURATION
// ============================================================
//
// Development:
//   - Local appsettings / appsettings.Development.json kullanılır.
//   - DATABASE_URL / DATABASE_PUBLIC_URL KULLANILMAZ.
//
// Production / Railway:
//   - Önce DATABASE_URL
//   - Yoksa DATABASE_PUBLIC_URL
//   - Railway connection string'e dönüştürülür.
// ============================================================

string? npgsqlFromRailway = null;

if (!builder.Environment.IsDevelopment())
{
    var railwayDbUrl =
        Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL");

    npgsqlFromRailway =
        PostgresConnectionStringHelper.ToNpgsqlConnectionString(railwayDbUrl);

    if (!string.IsNullOrWhiteSpace(npgsqlFromRailway))
    {
        // Configuration'da açıkça bir connection string verilmemişse
        // Railway connection string'ini kullan.
        var currentDefault =
            builder.Configuration.GetConnectionString("DefaultConnection");

        var currentAdmin =
            builder.Configuration.GetConnectionString("AdminConnection");

        if (string.IsNullOrWhiteSpace(currentDefault))
        {
            builder.Configuration[
                "ConnectionStrings:DefaultConnection"
            ] = npgsqlFromRailway;
        }

        if (string.IsNullOrWhiteSpace(currentAdmin))
        {
            builder.Configuration[
                "ConnectionStrings:AdminConnection"
            ] = npgsqlFromRailway;
        }
    }
}

// ============================================================
// RAILWAY / POSTGRES CONNECTION STRING NORMALIZATION
// ============================================================
//
// PostgreSQL URL veya klasik Npgsql connection string formatlarını
// normalize eder.
// ============================================================

if (!builder.Environment.IsDevelopment())
{
    foreach (var connKey in new[]
    {
        "DefaultConnection",
        "AdminConnection"
    })
    {
        var section = $"ConnectionStrings:{connKey}";
        var raw = builder.Configuration[section];

        if (PostgresConnectionStringHelper.TryNormalizeForNpgsql(
                raw,
                out var normalized)
            && !string.IsNullOrWhiteSpace(normalized))
        {
            builder.Configuration[section] = normalized;
        }
    }
}
// ============================================================
// ADMIN CONNECTION DATABASE NAME FALLBACK
// ============================================================
//
// Örneğin AdminConnection sadece:
//
//     admin_db
//
// şeklindeyse ve Railway connection mevcutsa,
// Railway host/user/password bilgileri korunarak database adı
// admin_db yapılır.
// ============================================================

var adminRaw =
    builder.Configuration["ConnectionStrings:AdminConnection"];

if (!string.IsNullOrWhiteSpace(npgsqlFromRailway)
    && PostgresConnectionStringHelper.LooksLikeDatabaseNameOnly(adminRaw))
{
    var merged =
        PostgresConnectionStringHelper.MergeDatabaseOntoNpgsqlBase(
            npgsqlFromRailway,
            adminRaw);

    if (!string.IsNullOrWhiteSpace(merged))
    {
        builder.Configuration[
            "ConnectionStrings:AdminConnection"
        ] = merged;
    }
}

// ============================================================
// DEFAULT CONNECTION FALLBACK
// ============================================================
//
// Production/Railway'de DefaultConnection çözülemediyse,
// Railway connection string'i son fallback olarak kullanılır.
//
// Development'ta npgsqlFromRailway null olduğu için burası
// local configuration'ı etkilemez.
// ============================================================

var defaultRaw =
    builder.Configuration["ConnectionStrings:DefaultConnection"];

var defaultOk =
    PostgresConnectionStringHelper.TryNormalizeForNpgsql(
        defaultRaw,
        out _);

if ((!defaultOk || string.IsNullOrWhiteSpace(defaultRaw))
    && !string.IsNullOrWhiteSpace(npgsqlFromRailway))
{
    builder.Configuration[
        "ConnectionStrings:DefaultConnection"
    ] = npgsqlFromRailway;
}

// ============================================================
// SERVICES — DATABASE
// ============================================================

var adminConnection =
    builder.Configuration.GetConnectionString("AdminConnection");

if (!string.IsNullOrWhiteSpace(adminConnection))
{
    builder.Services.AddDbContext<AdminDbContext>(options =>
        options.UseNpgsql(adminConnection));
}

builder.Services.AddScoped<TenantDbContextFactory>();

builder.Services.AddScoped<AppDbContext>(sp =>
    sp.GetRequiredService<TenantDbContextFactory>()
        .CreateDbContext());

// ============================================================
// MVC
// ============================================================

builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(
        0,
        new CariErinc.Helpers.TurkishDecimalBinderProvider());
})
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;

        options.JsonSerializerOptions.DictionaryKeyPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// ============================================================
// LOCALIZATION
// ============================================================

var trCulture = CultureInfo.GetCultureInfo("tr-TR");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture =
        new RequestCulture(trCulture);

    options.SupportedCultures =
        new[] { trCulture };

    options.SupportedUICultures =
        new[] { trCulture };
});

// ============================================================
// AUTHENTICATION
// ============================================================

builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "KirtasiyeAuth";

        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/auth/login";

        options.ExpireTimeSpan =
            TimeSpan.FromHours(8);

        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// ============================================================
// APPLICATION SERVICES
// ============================================================

builder.Services.AddScoped<
    CariErinc.Services.Interfaces.IDashboardService,
    CariErinc.Services.DashboardService>();

builder.Services.AddScoped<
    CariErinc.Services.Interfaces.IUrunService,
    CariErinc.Services.UrunService>();

builder.Services.AddScoped<
    CariErinc.Services.Interfaces.IUrunFiyatService,
    CariErinc.Services.UrunFiyatService>();

builder.Services.AddScoped<
    CariErinc.Services.Interfaces.IStokService,
    CariErinc.Services.StokService>();

builder.Services.AddScoped<
    CariErinc.Services.Interfaces.ICariService,
    CariErinc.Services.CariService>();

builder.Services.AddScoped<
    CariErinc.Services.Interfaces.IVeresiyeService,
    CariErinc.Services.VeresiyeService>();

builder.Services.AddScoped<
    CariErinc.Services.Interfaces.IAlisService,
    CariErinc.Services.AlisService>();

builder.Services.AddScoped<
    CariErinc.Services.Interfaces.IKasaService,
    CariErinc.Services.KasaService>();

builder.Services.AddScoped<
    CariErinc.Services.Interfaces.IRaporService,
    CariErinc.Services.RaporService>();

builder.Services.AddScoped<
    CariErinc.Services.Interfaces.ISatisService,
    CariErinc.Services.SatisService>();

builder.Services.AddScoped<
    CariErinc.Services.Interfaces.IGiderKategoriService,
    CariErinc.Services.GiderKategoriService>();

builder.Services.AddScoped<
    CariErinc.Services.Interfaces.IAuditLogService,
    CariErinc.Services.AuditLogService>();

builder.Services.AddScoped<
    CariErinc.Services.Interfaces.ILookupService,
    CariErinc.Services.LookupService>();

builder.Services.AddScoped<
    CariErinc.Services.Interfaces.IFaturaAnalizService,
    CariErinc.Services.FaturaAnalizService>();

// ============================================================
// YETKİ VE AYAR SERVİSLERİ
// ============================================================

builder.Services.AddMemoryCache();

builder.Services.AddSingleton<
    CariErinc.Services.Interfaces.IYetkiCacheService,
    CariErinc.Services.YetkiCacheService>();

builder.Services.AddSingleton<
    CariErinc.Services.Interfaces.IAyarService,
    CariErinc.Services.AyarService>();

// ============================================================
// BUILD APPLICATION
// ============================================================

var app = builder.Build();

// ============================================================
// MIDDLEWARE PIPELINE
// ============================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseMiddleware<SubdomainMiddleware>();

app.UseRouting();

app.UseRequestLocalization();

app.UseAuthentication();

app.UseAuthorization();

app.UseMiddleware<YetkiMiddleware>();

// ============================================================
// ROUTES
// ============================================================

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "auth",
    pattern: "auth/{action=login}",
    defaults: new
    {
        controller = "Auth"
    });

// ============================================================
// DATABASE HELPERS
// ============================================================

static async Task EnsurePostgreDatabaseExistsAsync(
    string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return;

    var builderConn =
        new Npgsql.NpgsqlConnectionStringBuilder(
            connectionString);

    var dbName = builderConn.Database;

    if (string.IsNullOrWhiteSpace(dbName))
        return;

    // PostgreSQL sistem database'ine bağlan.
    builderConn.Database = "postgres";

    await using var conn =
        new Npgsql.NpgsqlConnection(
            builderConn.ToString());

    await conn.OpenAsync();

    await using var cmd =
        conn.CreateCommand();

    cmd.CommandText =
        "SELECT 1 FROM pg_database WHERE datname = @name";

    cmd.Parameters.AddWithValue(
        "name",
        dbName);

    var exists =
        await cmd.ExecuteScalarAsync();

    if (exists == null || exists == DBNull.Value)
    {
        cmd.Parameters.Clear();

        // dbName connection string builder'dan geldiği için
        // identifier olarak quote ediyoruz.
        var safeDbName =
            dbName.Replace("\"", "\"\"");

        cmd.CommandText =
            $"CREATE DATABASE \"{safeDbName}\"";

        await cmd.ExecuteNonQueryAsync();
    }
}

// ============================================================
// POSTGRES IDENTITY SEQUENCE SYNC
// ============================================================

static async Task SyncAppIdentitySequencesAsync(
    AppDbContext db)
{
    await db.Database.OpenConnectionAsync();

    await using var cmd =
        db.Database.GetDbConnection()
            .CreateCommand();

    var tables = new[]
    {
        "IsletmeAyarlar",
        "Roller",
        "GiderKategoriler"
    };

    foreach (var table in tables)
    {
        cmd.CommandText =
            $"SELECT setval(" +
            $"pg_get_serial_sequence('\"{table}\"', 'Id'), " +
            $"COALESCE(MAX(\"Id\"), 0) + 1, false) " +
            $"FROM \"{table}\";";

        await cmd.ExecuteNonQueryAsync();
    }
}

// ============================================================
// DEFAULT ADMIN SEED
// ============================================================

static void SeedVarsayilanAdmin(
    AppDbContext db)
{
    if (!db.Kullanicilar.Any())
    {
        var adminKullanici =
            new CariErinc.Models.Kullanici
            {
                KullaniciAdi = "admin",

                SifreHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        "admin123"),

                AktifMi = true,

                OlusturulmaTarihi =
                    DateTime.UtcNow
            };

        db.Kullanicilar.Add(adminKullanici);

        db.SaveChanges();

        db.KullaniciRoller.Add(
            new CariErinc.Models.KullaniciRol
            {
                KullaniciId =
                    adminKullanici.Id,

                RolId = 1
            });

        db.SaveChanges();
    }
    else
    {
        var adminUser =
            db.Kullanicilar.FirstOrDefault(
                k => k.KullaniciAdi == "admin");

        if (adminUser != null
            && !db.KullaniciRoller.Any(
                kr =>
                    kr.KullaniciId == adminUser.Id
                    && kr.RolId == 1))
        {
            db.KullaniciRoller.Add(
                new CariErinc.Models.KullaniciRol
                {
                    KullaniciId =
                        adminUser.Id,

                    RolId = 1
                });

            db.SaveChanges();
        }
    }
}

// ============================================================
// DATABASE INITIALIZATION
// ============================================================

var defaultConnection =
    builder.Configuration
        .GetConnectionString("DefaultConnection");

adminConnection =
    builder.Configuration
        .GetConnectionString("AdminConnection");

var multiTenant =
    builder.Configuration
        .GetValue<bool>("MultiTenant:Enabled");

// ============================================================
// DATABASE EXISTENCE
// ============================================================
//
// Development:
//   Local DefaultConnection kullanılır.
//
// Production/Railway:
//   Railway connection kullanılır.
//
// MultiTenant:
//   Admin DB üzerinden tenant DB'leri bulunur.
// ============================================================

await EnsurePostgreDatabaseExistsAsync(
    defaultConnection);

if (!string.IsNullOrWhiteSpace(adminConnection))
{
    await EnsurePostgreDatabaseExistsAsync(
        adminConnection);
}

// ============================================================
// ADMIN DATABASE MIGRATION
// ============================================================

if (!string.IsNullOrWhiteSpace(adminConnection))
{
    using var scope =
        app.Services.CreateScope();

    var adminDb =
        scope.ServiceProvider
            .GetRequiredService<AdminDbContext>();

    await adminDb.Database.MigrateAsync();
}

// ============================================================
// SINGLE TENANT
// ============================================================

if (!multiTenant)
{
    using var scope =
        app.Services.CreateScope();

    var db =
        scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();

    await SyncAppIdentitySequencesAsync(db);

    SeedVarsayilanAdmin(db);
}

// ============================================================
// MULTI TENANT
// ============================================================

else if (!string.IsNullOrWhiteSpace(adminConnection))
{
    using var scope =
        app.Services.CreateScope();

    var adminDb =
        scope.ServiceProvider
            .GetRequiredService<AdminDbContext>();

    var tenantlar =
        await adminDb.TenantKayitlar
            .AsNoTracking()
            .Where(t =>
                t.AktifMi
                && t.ConnectionString != "")
            .ToListAsync();

    foreach (var tenant in tenantlar)
    {
        await EnsurePostgreDatabaseExistsAsync(
            tenant.ConnectionString);

        var opts =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(
                    tenant.ConnectionString)
                .Options;

        await using var tenantDb =
            new AppDbContext(opts);

        await tenantDb.Database.MigrateAsync();

        await SyncAppIdentitySequencesAsync(
            tenantDb);

        SeedVarsayilanAdmin(tenantDb);
    }
}

// ============================================================
// START
// ============================================================

app.Run();