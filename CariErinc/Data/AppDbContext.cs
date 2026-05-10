using Microsoft.EntityFrameworkCore;
using CariErinc.Models;

namespace CariErinc.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Urun> Urunler { get; set; }
    public DbSet<StokHareket> StokHareketler { get; set; }
    public DbSet<Musteri> Musteriler { get; set; }
    public DbSet<Cari> Cariler { get; set; }
    public DbSet<Veresiye> Veresiyeler { get; set; }
    public DbSet<VeresiyeOdeme> VeresiyeOdemeler { get; set; }
    public DbSet<Tedarikci> Tedarikciler { get; set; }
    public DbSet<Alis> Alislar { get; set; }
    public DbSet<AlisDetay> AlisDetaylari { get; set; }
    public DbSet<KasaHareket> KasaHareketler { get; set; }
    public DbSet<Satis> Satislar { get; set; }
    public DbSet<SatisDetay> SatisDetaylari { get; set; }
    public DbSet<Kullanici> Kullanicilar { get; set; }
    public DbSet<Rol> Roller { get; set; }
    public DbSet<KullaniciRol> KullaniciRoller { get; set; }
    public DbSet<RolYetki> RolYetkiler { get; set; }
    public DbSet<IsletmeAyar> IsletmeAyarlar { get; set; }
    public DbSet<GiderKategori> GiderKategoriler { get; set; }
    public DbSet<AuditLog> AuditLoglari { get; set; }
    public DbSet<AlisOdeme> AlisOdemeleri { get; set; }
    public DbSet<SatisIade> SatisIadeler { get; set; }
    public DbSet<SatisIadeDetay> SatisIadeDetaylari { get; set; }
    public DbSet<UrunFiyatAudit> UrunFiyatAuditlari { get; set; }
    public DbSet<FaturaEslesme> FaturaEslesmeleri { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Urun>()
            .Property(u => u.BirimFiyat)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Urun>()
            .Property(u => u.AlisFiyati)
            .HasPrecision(18, 2);

        modelBuilder.Entity<UrunFiyatAudit>()
            .Property(ua => ua.EskiFiyat)
            .HasPrecision(18, 2);

        modelBuilder.Entity<UrunFiyatAudit>()
            .Property(ua => ua.YeniFiyat)
            .HasPrecision(18, 2);

        modelBuilder.Entity<UrunFiyatAudit>()
            .HasOne(ua => ua.Urun)
            .WithMany()
            .HasForeignKey(ua => ua.UrunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Veresiye>()
            .Property(v => v.Tutar)
            .HasPrecision(18, 2);

        modelBuilder.Entity<VeresiyeOdeme>()
            .Property(vo => vo.OdemeTutari)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Alis>()
            .Property(a => a.ToplamTutar)
            .HasPrecision(18, 2);

        modelBuilder.Entity<AlisDetay>()
            .Property(ad => ad.BirimFiyat)
            .HasPrecision(18, 2);

        modelBuilder.Entity<KasaHareket>()
            .Property(kh => kh.Tutar)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Satis>()
            .Property(s => s.ToplamTutar)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Satis>()
            .Property(s => s.GenelIndirimTutari)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Satis>()
            .Property(s => s.IndirimSonrasiToplam)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Satis>()
            .Property(s => s.GenelIndirimOrani)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Satis>()
            .Property(s => s.GenelIndirimHedefToplam)
            .HasPrecision(18, 2);

        modelBuilder.Entity<SatisDetay>()
            .Property(sd => sd.BirimFiyat)
            .HasPrecision(18, 2);

        modelBuilder.Entity<SatisIadeDetay>()
            .Property(sid => sid.IadeTutari)
            .HasPrecision(18, 2);

        modelBuilder.Entity<SatisDetay>()
            .Property(sd => sd.IndirimTutari)
            .HasPrecision(18, 2);

        modelBuilder.Entity<SatisDetay>()
            .Property(sd => sd.NetTutar)
            .HasPrecision(18, 2);

        modelBuilder.Entity<SatisDetay>()
            .Property(sd => sd.IndirimOrani)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Satis>()
            .HasOne(s => s.Veresiye)
            .WithOne(v => v.Satis)
            .HasForeignKey<Veresiye>(v => v.SatisId)
            .IsRequired(false);

        modelBuilder.Entity<Cari>()
            .Property(c => c.Rol)
            .HasConversion<int>();

        modelBuilder.Entity<Musteri>()
            .HasOne(m => m.Cari)
            .WithMany()
            .HasForeignKey(m => m.CariId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Tedarikci>()
            .HasOne(t => t.Cari)
            .WithMany()
            .HasForeignKey(t => t.CariId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Satis>()
            .HasOne(s => s.Cari)
            .WithMany()
            .HasForeignKey(s => s.CariId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Veresiye>()
            .HasOne(v => v.Cari)
            .WithMany()
            .HasForeignKey(v => v.CariId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Alis>()
            .HasOne(a => a.Cari)
            .WithMany()
            .HasForeignKey(a => a.CariId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Urun>()
            .HasOne(u => u.Cari)
            .WithMany()
            .HasForeignKey(u => u.CariId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<KasaHareket>()
            .HasOne(kh => kh.GiderKategori)
            .WithMany()
            .HasForeignKey(kh => kh.GiderKategoriId)
            .OnDelete(DeleteBehavior.SetNull);

        // Kullanici ↔ Rol M:N
        modelBuilder.Entity<KullaniciRol>()
            .HasKey(kr => new { kr.KullaniciId, kr.RolId });

        modelBuilder.Entity<KullaniciRol>()
            .HasOne(kr => kr.Kullanici)
            .WithMany(k => k.KullaniciRoller)
            .HasForeignKey(kr => kr.KullaniciId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<KullaniciRol>()
            .HasOne(kr => kr.Rol)
            .WithMany(r => r.KullaniciRoller)
            .HasForeignKey(kr => kr.RolId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RolYetki>()
            .HasOne(ry => ry.Rol)
            .WithMany(r => r.Yetkiler)
            .HasForeignKey(ry => ry.RolId)
            .OnDelete(DeleteBehavior.Cascade);

        // IsletmeAyar Unique index
        modelBuilder.Entity<IsletmeAyar>()
            .HasIndex(a => a.Anahtar)
            .IsUnique();

        // Seed: Admin rolü
        modelBuilder.Entity<Rol>().HasData(
            new Rol { Id = 1, Ad = "Admin", Aciklama = "Tam yetkili yönetici", IsAdmin = true }
        );

        // Seed: İşletme ayarları
        modelBuilder.Entity<IsletmeAyar>().HasData(
            new IsletmeAyar { Id = 1, Anahtar = "DukkanAdi", Deger = "Kırtasiye Dükkanı" },
            new IsletmeAyar { Id = 2, Anahtar = "IsletmeTipi", Deger = "Kirtasiye" },
            new IsletmeAyar { Id = 3, Anahtar = "Adres", Deger = "" },
            new IsletmeAyar { Id = 4, Anahtar = "Telefon", Deger = "" },
            new IsletmeAyar { Id = 5, Anahtar = "VarsayilanKdv", Deger = "20" }
        );

        modelBuilder.Entity<GiderKategori>().HasData(
            new GiderKategori { Id = 1, Ad = "Satış", Tip = KasaHareketTipi.Gelir, SilinebilirMi = false },
            new GiderKategori { Id = 2, Ad = "Veresiye Ödeme", Tip = KasaHareketTipi.Gelir, SilinebilirMi = false },
            new GiderKategori { Id = 3, Ad = "Alış", Tip = KasaHareketTipi.Gider, SilinebilirMi = false },
            new GiderKategori { Id = 4, Ad = "Kira", Tip = KasaHareketTipi.Gider, SilinebilirMi = true },
            new GiderKategori { Id = 5, Ad = "Fatura", Tip = KasaHareketTipi.Gider, SilinebilirMi = true },
            new GiderKategori { Id = 6, Ad = "Maaş", Tip = KasaHareketTipi.Gider, SilinebilirMi = true },
            new GiderKategori { Id = 7, Ad = "Diğer", Tip = KasaHareketTipi.Gider, SilinebilirMi = true },
            new GiderKategori { Id = 8, Ad = "Satış İadesi", Tip = KasaHareketTipi.Gider, SilinebilirMi = false },
            new GiderKategori { Id = 9, Ad = "Alış İadesi", Tip = KasaHareketTipi.Gelir, SilinebilirMi = false }
        );
    }
}
