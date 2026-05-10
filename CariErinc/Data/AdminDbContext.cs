using CariErinc.Models;
using Microsoft.EntityFrameworkCore;

namespace CariErinc.Data;

public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options)
        : base(options)
    {
    }

    public DbSet<TenantKayit> TenantKayitlar { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantKayit>(e =>
        {
            e.HasIndex(t => t.Subdomain).IsUnique();
            // Program.cs ile aynı: Npgsql.EnableLegacyTimestampBehavior — snapshot/runtime uyumu
            e.Property(t => t.OlusturulmaTarihi)
                .HasColumnType("timestamp without time zone");
        });
    }
}
