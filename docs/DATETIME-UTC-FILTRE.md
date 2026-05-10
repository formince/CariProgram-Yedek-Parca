# Tarih filtreleri ve PostgreSQL (Npgsql)

Liste/rapor filtrelerinde `baslangic` / `bitis` kullanılırken **sorgu parametreleri** `DateTimeKind.Utc` olmalıdır; aksi halde Npgsql `timestamptz` ile `Unspecified` DateTime yazmayı reddeder.

**Tek kaynak:** [`DateTimeUtcFilter`](../CariErinc/Helpers/DateTimeUtcFilter.cs) — yeni repository filtreleri burayı kullanmalıdır.
