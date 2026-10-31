# DilekGiyim

Dilek Giyim için e-ticaret / tanıtım web sitesi.

| Katman   | Teknoloji                         | Klasör      |
|----------|-----------------------------------|-------------|
| Backend  | .NET 10 (ASP.NET Core Web API)    | `backend/`  |
| Frontend | Angular 22 (standalone, signals)  | `frontend/` |

## Klasör yapısı

```
DilekGiyim/
├── backend/
│   ├── DilekGiyim.slnx
│   └── DilekGiyim.Api/
│       ├── Controllers/      # API controller'ları
│       ├── Models/           # Veri modelleri
│       └── Program.cs        # Uygulama girişi, CORS, OpenAPI
└── frontend/
    ├── proxy.conf.json       # /api isteklerini backend'e yönlendirir
    └── src/app/
        ├── pages/            # Sayfalar (home, ...)
        ├── services/         # API servisleri
        └── models/           # TypeScript arayüzleri
```

## Gereksinimler

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) 22 veya üzeri (LTS)

## Çalıştırma

İki ayrı terminalde:

**1. Backend** (http://localhost:5080)

```bash
cd backend/DilekGiyim.Api
dotnet run
```

Uç noktalar:
- `GET /api/health`
- `GET /api/products`
- `GET /api/products/{id}`
- `GET /openapi/v1.json` (yalnızca Development)

**2. Frontend** (http://localhost:4200)

```bash
cd frontend
npm install
npm start
```

Geliştirme sırasında frontend'deki `/api/...` istekleri `proxy.conf.json` ile backend'e gider.

## Yol haritası

- [ ] Veritabanı (EF Core) ve gerçek ürün verisi
- [ ] Ürün detay, kategori ve arama sayfaları
- [ ] Sepet ve sipariş
- [ ] Yönetim paneli
- [ ] Hosting ve yayına alma
