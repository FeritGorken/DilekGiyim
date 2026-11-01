# DilekGiyim

Dilek Giyim için e-ticaret sitesi ve yönetim paneli.

| Katman   | Teknoloji                                   | Klasör      |
|----------|---------------------------------------------|-------------|
| Backend  | .NET 10 Minimal API + EF Core (SQLite)      | `backend/`  |
| Frontend | Angular 22 (standalone, signals, zoneless)  | `frontend/` |

## Neler var?

**Mağaza** (`/`)
- Ana sayfa: banner, öne çıkanlar (Çok Satanlar / Yeni Gelenler / İndirimdekiler), detay bandı, kategoriler
- Kategori, arama ve tüm ürünler listesi (`/kategori/:slug`, `/urunler?q=`)
- Ürün detay: galeri, renk ve beden seçimi, bedene göre stok uyarısı (`/urun/:slug`)
- Sepet: adet değiştirme, indirim kodu, ücretsiz kargo çubuğu (`/sepet`)
- Teslimat ve ödeme: sipariş oluşturma (`/odeme`)

**Yönetim paneli** (`/admin`)
- Genel bakış: ciro, sipariş, ortalama sepet, kritik stok, günlük satış, son siparişler
- Siparişler: duruma göre filtre, detay, durum ve kargo takip no güncelleme
- Stok takibi: renk/beden bazında stok, hızlı düzeltme, stok girişi, stok hareketleri
- Ürünler: ekle/düzenle, görseller, fiyat-maliyet-kâr marjı, renk × beden stok tablosu
- Fiyat, kampanya ve sepet: toplu fiyat güncelleme (önizlemeli), kampanyalar, indirim kodları, kargo ve sepet kuralları

## İş kuralları

- **Fiyat her zaman sunucuda hesaplanır.** Tarayıcı sepette yalnızca varyant ve adet tutar; tutar, indirim ve kargo `/api/cart/quote` ile gelir.
- **Stok varyant (renk + beden) bazında tutulur.** Sipariş oluşunca stoktan düşer; iptal veya iadede stoğa geri eklenir. Her değişiklik "stok hareketi" olarak kaydedilir.
- **Kritik stok:** varyant stoğu ürünün "kritik eşiği"ne eşit veya altındaysa kritik, 0 ise tükendi sayılır.
- **Sitede "Son X ürün" uyarısı** ürün kartındaki eşik değerine göre gösterilir; gerçek stok miktarı müşteriye açılmaz.
- **İndirim kodu:** yüzde veya tutar, alt sepet limiti, kullanım limiti ve tarih aralığı desteklenir.

## Klasör yapısı

```
backend/DilekGiyim.Api/
├── Domain/          # Product, ProductVariant, Order, Coupon, StockMovement, StoreSettings
├── Data/            # AppDbContext, SeedData (örnek veri)
├── Contracts/       # API'nin aldığı/döndürdüğü DTO'lar
├── Services/        # PricingService (sepet hesabı), OrderService (sipariş + stok)
├── Endpoints/       # StoreEndpoints (/api/...), AdminEndpoints (/api/admin/...)
└── Auth/            # AdminKeyFilter

frontend/src/app/
├── core/            # modeller, sepet ve katalog servisleri, TL biçimlendirme
├── store/           # mağaza sayfaları
└── admin/           # yönetim paneli sayfaları
```

## Çalıştırma

Gereksinimler: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), [Node.js](https://nodejs.org/) 22+

**1. Backend** (http://localhost:5080)

```bash
cd backend/DilekGiyim.Api
dotnet run
```

İlk çalıştırmada `dilekgiyim.db` (SQLite) oluşturulur ve örnek ürünlerle doldurulur. Sıfırlamak için dosyayı silip tekrar çalıştırın.

**2. Frontend** (http://localhost:4200)

```bash
cd frontend
npm install
npm start
```

Geliştirmede `/api` istekleri `proxy.conf.json` ile backend'e gider.

**Yönetim paneli:** http://localhost:4200/admin
Geliştirme anahtarı: `gelistirme-admin-anahtari` (`appsettings.Development.json` içinde).

## Canlıya çıkmadan önce yapılacaklar

- [ ] **Admin girişi:** Şu anki anahtarlı giriş geçici. Kullanıcı adı/şifreli gerçek kimlik doğrulama eklenmeli. Canlıda `Admin__ApiKey` ortam değişkeni güçlü bir değerle verilmeli (`appsettings.json` içinde boş bırakıldı).
- [ ] **Ödeme altyapısı:** Kredi kartı seçeneği kapalı. iyzico / PayTR gibi bir sağlayıcı bağlanmalı.
- [ ] **Veritabanı:** SQLite geliştirme içindir. Canlıda SQL Server veya PostgreSQL'e geçilip EF Core migration'ları kullanılmalı (`EnsureCreated` yerine).
- [ ] **Görsel yükleme:** Ürün görselleri şimdilik adres (URL) olarak ekleniyor; dosya yükleme uç noktası eklenmeli.
- [ ] **E-posta:** Sipariş onayı ve terk edilen sepet hatırlatması için e-posta servisi.
- [ ] Mesafeli satış sözleşmesi, KVKK metinleri ve iletişim bilgileri.
