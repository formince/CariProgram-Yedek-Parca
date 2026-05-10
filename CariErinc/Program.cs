using System.Globalization;
using System.Linq;
using CariErinc.Data;
using CariErinc.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

// Railway: DATABASE_URL environment variable'ını configuration'a at
var railwayDbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrWhiteSpace(railwayDbUrl))
{
    // Eğer appsettings'de boş ise, DATABASE_URL'yi kullan
    var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(defaultConn))
        builder.Configuration["ConnectionStrings:DefaultConnection"] = railwayDbUrl;

    var railwayAdminConn = builder.Configuration.GetConnectionString("AdminConnection");
    if (string.IsNullOrWhiteSpace(railwayAdminConn))
        builder.Configuration["ConnectionStrings:AdminConnection"] = railwayDbUrl;
}

// Services — uygulama DB (isteğe bağlı tenant connection ile factory)
var adminConnection = builder.Configuration.GetConnectionString("AdminConnection");
if (!string.IsNullOrEmpty(adminConnection))
{
    builder.Services.AddDbContext<AdminDbContext>(options =>
        options.UseNpgsql(adminConnection));
}

builder.Services.AddScoped<TenantDbContextFactory>();
builder.Services.AddScoped<AppDbContext>(sp =>
    sp.GetRequiredService<TenantDbContextFactory>().CreateDbContext());

builder.Services.AddControllersWithViews(options =>
    {
        options.ModelBinderProviders.Insert(0, new CariErinc.Helpers.TurkishDecimalBinderProvider());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

var trCulture = CultureInfo.GetCultureInfo("tr-TR");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(trCulture);
    options.SupportedCultures = new[] { trCulture };
    options.SupportedUICultures = new[] { trCulture };
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "KirtasiyeAuth";
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/auth/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<CariErinc.Services.Interfaces.IDashboardService, CariErinc.Services.DashboardService>();
builder.Services.AddScoped<CariErinc.Services.Interfaces.IUrunService, CariErinc.Services.UrunService>();
builder.Services.AddScoped<CariErinc.Services.Interfaces.IUrunFiyatService, CariErinc.Services.UrunFiyatService>();
builder.Services.AddScoped<CariErinc.Services.Interfaces.IStokService, CariErinc.Services.StokService>();
builder.Services.AddScoped<CariErinc.Services.Interfaces.ICariService, CariErinc.Services.CariService>();
builder.Services.AddScoped<CariErinc.Services.Interfaces.IVeresiyeService, CariErinc.Services.VeresiyeService>();
builder.Services.AddScoped<CariErinc.Services.Interfaces.IAlisService, CariErinc.Services.AlisService>();
builder.Services.AddScoped<CariErinc.Services.Interfaces.IKasaService, CariErinc.Services.KasaService>();
builder.Services.AddScoped<CariErinc.Services.Interfaces.IRaporService, CariErinc.Services.RaporService>();
builder.Services.AddScoped<CariErinc.Services.Interfaces.ISatisService, CariErinc.Services.SatisService>();
builder.Services.AddScoped<CariErinc.Services.Interfaces.IGiderKategoriService, CariErinc.Services.GiderKategoriService>();
builder.Services.AddScoped<CariErinc.Services.Interfaces.IAuditLogService, CariErinc.Services.AuditLogService>();
builder.Services.AddScoped<CariErinc.Services.Interfaces.ILookupService, CariErinc.Services.LookupService>();
builder.Services.AddScoped<CariErinc.Services.Interfaces.IFaturaAnalizService, CariErinc.Services.FaturaAnalizService>();

// Yetki ve Ayar servisleri
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CariErinc.Services.Interfaces.IYetkiCacheService, CariErinc.Services.YetkiCacheService>();
builder.Services.AddSingleton<CariErinc.Services.Interfaces.IAyarService, CariErinc.Services.AyarService>();

var app = builder.Build();

// Middleware pipeline
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "auth",
    pattern: "auth/{action=login}",
    defaults: new { controller = "Auth" });

// Veritabanı yoksa oluştur (PostgreSQL)
static async Task EnsurePostgreDatabaseExistsAsync(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return;

    var builderConn = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
    var dbName = builderConn.Database;
    builderConn.Database = "postgres";
    await using var conn = new Npgsql.NpgsqlConnection(builderConn.ToString());
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
    cmd.Parameters.AddWithValue("name", dbName ?? "");
    var exists = await cmd.ExecuteScalarAsync();
    if (exists == null || exists == DBNull.Value)
    {
        cmd.Parameters.Clear();
        cmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
        await cmd.ExecuteNonQueryAsync();
    }
}

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
var adminConn = builder.Configuration.GetConnectionString("AdminConnection");
var multiTenant = builder.Configuration.GetValue<bool>("MultiTenant:Enabled");

try
{
    await EnsurePostgreDatabaseExistsAsync(defaultConnection);
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Could not ensure database exists: {ex.Message}");
}

if (!string.IsNullOrEmpty(adminConn))
{
    try
    {
        await EnsurePostgreDatabaseExistsAsync(adminConn);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not ensure admin database exists: {ex.Message}");
    }
}

if (!string.IsNullOrEmpty(adminConn))
{
    using (var scope = app.Services.CreateScope())
    {
        var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        await adminDb.Database.MigrateAsync();
    }
}

static async Task SyncAppIdentitySequencesAsync(AppDbContext db)
{
    await db.Database.OpenConnectionAsync();
    await using var cmd = db.Database.GetDbConnection().CreateCommand();
    var tables = new[] { "IsletmeAyarlar", "Roller", "GiderKategoriler" };
    foreach (var table in tables)
    {
        cmd.CommandText = $"SELECT setval(pg_get_serial_sequence('\"{table}\"', 'Id'), COALESCE(MAX(\"Id\"), 0) + 1, false) FROM \"{table}\";";
        await cmd.ExecuteNonQueryAsync();
    }
}

static void SeedVarsayilanAdmin(AppDbContext db)
{
    if (!db.Kullanicilar.Any())
    {
        var adminKullanici = new CariErinc.Models.Kullanici
        {
            KullaniciAdi = "admin",
            SifreHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            AktifMi = true,
            OlusturulmaTarihi = DateTime.UtcNow
        };
        db.Kullanicilar.Add(adminKullanici);
        db.SaveChanges();

        db.KullaniciRoller.Add(new CariErinc.Models.KullaniciRol
        {
            KullaniciId = adminKullanici.Id,
            RolId = 1
        });
        db.SaveChanges();
    }
    else
    {
        var adminUser = db.Kullanicilar.FirstOrDefault(k => k.KullaniciAdi == "admin");
        if (adminUser != null && !db.KullaniciRoller.Any(kr => kr.KullaniciId == adminUser.Id && kr.RolId == 1))
        {
            db.KullaniciRoller.Add(new CariErinc.Models.KullaniciRol
            {
                KullaniciId = adminUser.Id,
                RolId = 1
            });
            db.SaveChanges();
        }
    }
}

if (!multiTenant)
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await SyncAppIdentitySequencesAsync(db);
        SeedVarsayilanAdmin(db);
    }
}
else if (!string.IsNullOrEmpty(adminConn))
{
    using (var scope = app.Services.CreateScope())
    {
        var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var tenantlar = await adminDb.TenantKayitlar.AsNoTracking()
            .Where(t => t.AktifMi && t.ConnectionString != "")
            .ToListAsync();

        foreach (var tenant in tenantlar)
        {
            await EnsurePostgreDatabaseExistsAsync(tenant.ConnectionString);
            var opts = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(tenant.ConnectionString).Options;
            await using var tenantDb = new AppDbContext(opts);
            await tenantDb.Database.MigrateAsync();
            await SyncAppIdentitySequencesAsync(tenantDb);
            SeedVarsayilanAdmin(tenantDb);
        }
    }
}

app.Run();
