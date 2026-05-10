using System.Globalization;
using CariErinc.Data;
using CariErinc.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);


// Services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

// Veritabanı yoksa oluştur
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    var builderConn = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
    var dbName = builderConn.Database;
    builderConn.Database = "postgres";
    await using (var conn = new Npgsql.NpgsqlConnection(builderConn.ToString()))
    {
        await conn.OpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
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
    }
}

// Migration uygula (tablolar yoksa)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // PostgreSQL Identity/Sequence Sync (Manuel Seed ID çakışmasını önlemek için)
    await using (var cmd = db.Database.GetDbConnection().CreateCommand())
    {
        await db.Database.OpenConnectionAsync();
        var tables = new[] { "IsletmeAyarlar", "Roller", "GiderKategoriler" };
        foreach (var table in tables)
        {
            cmd.CommandText = $"SELECT setval(pg_get_serial_sequence('\"{table}\"', 'Id'), COALESCE(MAX(\"Id\"), 0) + 1, false) FROM \"{table}\";";
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

// Seed: İlk kullanıcı yoksa oluştur ve Admin rolünü ata
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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

        // Admin rolü seed'den geliyor (Id=1), kullanıcıya ata
        db.KullaniciRoller.Add(new CariErinc.Models.KullaniciRol
        {
            KullaniciId = adminKullanici.Id,
            RolId = 1
        });
        db.SaveChanges();
    }
    else
    {
        // Mevcut admin kullanıcısına admin rolü yoksa ekle
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

app.Run();
