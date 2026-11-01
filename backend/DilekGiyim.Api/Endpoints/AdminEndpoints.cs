using System.Globalization;
using System.Text;
using DilekGiyim.Api.Auth;
using DilekGiyim.Api.Contracts;
using DilekGiyim.Api.Data;
using DilekGiyim.Api.Domain;
using DilekGiyim.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace DilekGiyim.Api.Endpoints;

/// <summary>Yönetim paneli uç noktaları. Hepsi admin oturumu ister (AdminAuthEndpoints ile giriş).</summary>
public static class AdminEndpoints
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization(AuthSetup.AdminPolicy)
            .WithTags("Admin");

        MapDashboard(admin);
        MapOrders(admin);
        MapStock(admin);
        MapProducts(admin);
        MapPricing(admin);
    }

    // ---------------- Genel bakış ----------------

    private static void MapDashboard(RouteGroupBuilder admin)
    {
        admin.MapGet("/dashboard", async (AppDbContext db, int? days) =>
        {
            var period = Math.Clamp(days ?? 30, 1, 365);
            var now = DateTime.UtcNow;
            var from = now.AddDays(-period);
            var prevFrom = from.AddDays(-period);

            var orders = await db.Orders.AsNoTracking().Include(o => o.Lines)
                .Where(o => o.CreatedAt >= prevFrom).ToListAsync();
            static bool Counts(Order o) => o.Status is not (OrderStatus.Cancelled or OrderStatus.Returned or OrderStatus.AwaitingPayment);

            var current = orders.Where(o => o.CreatedAt >= from && Counts(o)).ToList();
            var previous = orders.Where(o => o.CreatedAt < from && Counts(o)).ToList();
            var revenue = current.Sum(o => o.Total);
            var prevRevenue = previous.Sum(o => o.Total);
            var change = prevRevenue == 0 ? 0 : Math.Round((revenue - prevRevenue) / prevRevenue * 100, 1);

            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, Istanbul));
            var daily = Enumerable.Range(0, 14).Select(i => today.AddDays(i - 13)).Select(d => new DailySalesDto(d,
                current.Concat(previous)
                    .Where(o => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(o.CreatedAt, Istanbul)) == d)
                    .Sum(o => o.Total))).ToList();

            var stockRows = await LoadStockRowsAsync(db);
            var critical = stockRows.Where(r => r.Status != "ok").OrderBy(r => r.Stock).ToList();

            var pending = await db.Orders.CountAsync(o => o.Status == OrderStatus.New || o.Status == OrderStatus.Preparing);
            var recent = await db.Orders.AsNoTracking().Include(o => o.Lines)
                .OrderByDescending(o => o.CreatedAt).Take(5).ToListAsync();

            var top = current.SelectMany(o => o.Lines)
                .GroupBy(l => l.ProductName)
                .Select(g => new TopProductDto(g.Key, g.Sum(l => l.Quantity)))
                .OrderByDescending(t => t.Quantity).Take(4).ToList();

            return new DashboardDto(
                revenue, change, current.Count, pending,
                current.Count == 0 ? 0 : Math.Round(revenue / current.Count, 2),
                critical.Count(r => r.Status == "critical"), critical.Count(r => r.Status == "out"),
                daily, critical.Take(6).ToList(), recent.Select(ToSummary).ToList(), top);
        });
    }

    // ---------------- Siparişler ----------------

    private static void MapOrders(RouteGroupBuilder admin)
    {
        admin.MapGet("/orders", async (AppDbContext db, OrderStatus? status, string? q) =>
        {
            var query = db.Orders.AsNoTracking().Include(o => o.Lines).AsQueryable();
            if (status is not null) query = query.Where(o => o.Status == status);
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(o => o.OrderNo.Contains(q) || o.CustomerName.Contains(q) || o.Email.Contains(q));
            var list = await query.OrderByDescending(o => o.CreatedAt).Take(200).ToListAsync();
            return list.Select(ToSummary).ToList();
        });

        admin.MapGet("/orders/counts", async (AppDbContext db) =>
            await db.Orders.GroupBy(o => o.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync());

        admin.MapGet("/orders/{id:int}", async (AppDbContext db, int id) =>
        {
            var o = await db.Orders.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
            return o is null ? Results.NotFound(new ErrorDto("Sipariş bulunamadı.")) : Results.Ok(ToDetail(o));
        });

        admin.MapPut("/orders/{id:int}/status", async (AppDbContext db, OrderService svc, int id, UpdateOrderStatusRequest req) =>
        {
            var o = await db.Orders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
            if (o is null) return Results.NotFound(new ErrorDto("Sipariş bulunamadı."));
            var error = await svc.ChangeStatusAsync(o, req.Status, req.TrackingNo, req.PaymentState);
            return error is null ? Results.Ok(ToDetail(o)) : Results.BadRequest(new ErrorDto(error));
        });
    }

    // ---------------- Stok ----------------

    private static void MapStock(RouteGroupBuilder admin)
    {
        admin.MapGet("/stock", async (AppDbContext db, string? filter, string? q) =>
        {
            var all = await LoadStockRowsAsync(db);
            var summary = new StockSummaryDto(
                all.Select(r => r.ProductId).Distinct().Count(), all.Count, all.Sum(r => r.Stock),
                all.Count(r => r.Status == "critical"), all.Count(r => r.Status == "out"));

            IEnumerable<StockRowDto> rows = all;
            if (filter is "critical" or "out" or "ok") rows = rows.Where(r => r.Status == filter);
            if (!string.IsNullOrWhiteSpace(q))
                rows = rows.Where(r => r.ProductName.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Sku.Contains(q, StringComparison.OrdinalIgnoreCase));
            return new StockListDto(summary, rows.ToList());
        });

        admin.MapPost("/stock/{variantId:int}/adjust", async (AppDbContext db, int variantId, StockAdjustRequest req) =>
        {
            if (req.Delta == 0) return Results.BadRequest(new ErrorDto("Değişiklik miktarı 0 olamaz."));
            if (req.Type is StockMovementType.Sale or StockMovementType.Cancel)
                return Results.BadRequest(new ErrorDto("Satış ve iptal hareketleri siparişlerden otomatik oluşur."));

            var v = await db.Variants.Include(x => x.Product).FirstOrDefaultAsync(x => x.Id == variantId);
            if (v is null) return Results.NotFound(new ErrorDto("Varyant bulunamadı."));
            if (v.Stock + req.Delta < 0) return Results.BadRequest(new ErrorDto("Stok sıfırın altına düşemez."));

            v.Stock += req.Delta;
            db.StockMovements.Add(new StockMovement { VariantId = v.Id, Delta = req.Delta, Type = req.Type, Note = req.Note });
            await db.SaveChangesAsync();
            return Results.Ok(ToStockRow(v, v.Product!));
        });

        admin.MapGet("/stock/movements", async (AppDbContext db, int? take) =>
            await db.StockMovements.AsNoTracking()
                .Include(m => m.Variant!).ThenInclude(v => v.Product)
                .OrderByDescending(m => m.CreatedAt).Take(Math.Clamp(take ?? 20, 1, 200))
                .Select(m => new StockMovementDto(m.Id, m.CreatedAt, m.Variant!.Product!.Name, m.Variant.ColorName,
                    m.Variant.Size, m.Delta, m.Type, m.Reference, m.Note))
                .ToListAsync());
    }

    // ---------------- Ürünler ----------------

    private static void MapProducts(RouteGroupBuilder admin)
    {
        admin.MapGet("/categories", async (AppDbContext db) =>
            await db.Categories.OrderBy(c => c.SortOrder).Select(c => new CategoryDto(c.Id, c.Name, c.Slug)).ToListAsync());

        admin.MapGet("/products", async (AppDbContext db) =>
        {
            var list = await db.Products.AsNoTracking()
                .Include(p => p.Category).Include(p => p.Images).Include(p => p.Variants)
                .OrderBy(p => p.Name).ToListAsync();
            return list.Select(p => new AdminProductListItemDto(
                p.Id, p.Name, p.Code, p.Category!.Name, p.Price, p.SalePrice,
                p.Variants.Sum(v => v.Stock), p.Variants.Count, p.IsPublished,
                p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault())).ToList();
        });

        admin.MapGet("/products/{id:int}", async (AppDbContext db, int id) =>
        {
            var p = await db.Products.AsNoTracking().Include(x => x.Images).Include(x => x.Variants)
                .FirstOrDefaultAsync(x => x.Id == id);
            return p is null ? Results.NotFound(new ErrorDto("Ürün bulunamadı.")) : Results.Ok(ToAdminProduct(p));
        });

        admin.MapPost("/products", async (AppDbContext db, AdminProductDto req) =>
        {
            var p = new Product();
            var error = await ApplyProductAsync(db, p, req);
            if (error is not null) return Results.BadRequest(new ErrorDto(error));
            db.Products.Add(p);
            await db.SaveChangesAsync();
            return Results.Ok(ToAdminProduct(p));
        });

        admin.MapPut("/products/{id:int}", async (AppDbContext db, int id, AdminProductDto req) =>
        {
            var p = await db.Products.Include(x => x.Images).Include(x => x.Variants).FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound(new ErrorDto("Ürün bulunamadı."));
            var error = await ApplyProductAsync(db, p, req);
            if (error is not null) return Results.BadRequest(new ErrorDto(error));
            await db.SaveChangesAsync();
            return Results.Ok(ToAdminProduct(p));
        });

        admin.MapDelete("/products/{id:int}", async (AppDbContext db, int id) =>
        {
            var p = await db.Products.FindAsync(id);
            if (p is null) return Results.NotFound(new ErrorDto("Ürün bulunamadı."));
            db.Products.Remove(p);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    // ---------------- Fiyat, kampanya, kupon, sepet ayarları ----------------

    private static void MapPricing(RouteGroupBuilder admin)
    {
        admin.MapPost("/prices/bulk", async (AppDbContext db, BulkPriceRequest req) =>
        {
            if (req.Percent <= 0 || req.Percent >= 100)
                return Results.BadRequest(new ErrorDto("Oran 0 ile 100 arasında olmalı."));

            var query = db.Products.AsQueryable();
            if (req.CategoryId is not null) query = query.Where(p => p.CategoryId == req.CategoryId);
            var products = await query.OrderBy(p => p.Name).ToListAsync();

            var factor = req.Increase ? 1 + req.Percent / 100m : 1 - req.Percent / 100m;
            var rows = products.Select(p =>
            {
                var raw = p.Price * factor;
                var next = req.RoundTo90 ? Math.Max(9.90m, Math.Floor(raw / 10m) * 10m + 9.90m) : Math.Round(raw, 2);
                return new BulkPriceRowDto(p.Id, p.Name, p.Price, next);
            }).ToList();

            if (req.Apply)
            {
                foreach (var p in products)
                {
                    p.Price = rows.First(r => r.ProductId == p.Id).New;
                    if (p.SalePrice is not null && p.SalePrice >= p.Price) p.SalePrice = null;
                }
                await db.SaveChangesAsync();
            }
            return Results.Ok(rows);
        });

        admin.MapGet("/coupons", async (AppDbContext db) =>
            await db.Coupons.AsNoTracking().OrderBy(c => c.Code).Select(c => ToCoupon(c)).ToListAsync());

        admin.MapPost("/coupons", async (AppDbContext db, CouponDto req) =>
        {
            var c = new Coupon();
            var error = await ApplyCouponAsync(db, c, req);
            if (error is not null) return Results.BadRequest(new ErrorDto(error));
            db.Coupons.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(ToCoupon(c));
        });

        admin.MapPut("/coupons/{id:int}", async (AppDbContext db, int id, CouponDto req) =>
        {
            var c = await db.Coupons.FindAsync(id);
            if (c is null) return Results.NotFound(new ErrorDto("Kod bulunamadı."));
            var error = await ApplyCouponAsync(db, c, req);
            if (error is not null) return Results.BadRequest(new ErrorDto(error));
            await db.SaveChangesAsync();
            return Results.Ok(ToCoupon(c));
        });

        admin.MapDelete("/coupons/{id:int}", async (AppDbContext db, int id) =>
        {
            var c = await db.Coupons.FindAsync(id);
            if (c is null) return Results.NotFound(new ErrorDto("Kod bulunamadı."));
            db.Coupons.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        admin.MapGet("/campaigns", async (AppDbContext db) =>
            await db.Campaigns.AsNoTracking().OrderByDescending(c => c.StartsAt)
                .Select(c => new CampaignDto(c.Id, c.Name, c.Description, c.StartsAt, c.EndsAt, c.IsActive)).ToListAsync());

        admin.MapPost("/campaigns", async (AppDbContext db, CampaignDto req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest(new ErrorDto("Kampanya adı gerekli."));
            if (req.EndsAt < req.StartsAt) return Results.BadRequest(new ErrorDto("Bitiş tarihi başlangıçtan önce olamaz."));
            var c = new Campaign { Name = req.Name.Trim(), Description = req.Description, StartsAt = req.StartsAt, EndsAt = req.EndsAt, IsActive = req.IsActive };
            db.Campaigns.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(new CampaignDto(c.Id, c.Name, c.Description, c.StartsAt, c.EndsAt, c.IsActive));
        });

        admin.MapDelete("/campaigns/{id:int}", async (AppDbContext db, int id) =>
        {
            var c = await db.Campaigns.FindAsync(id);
            if (c is null) return Results.NotFound(new ErrorDto("Kampanya bulunamadı."));
            db.Campaigns.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        admin.MapGet("/settings", async (AppDbContext db) =>
        {
            var s = await db.Settings.AsNoTracking().FirstAsync();
            return new SettingsDto(s.FreeShippingThreshold, s.ShippingFee, s.MinOrderTotal, s.MaxQuantityPerItem,
                s.ShowFreeShippingBar, s.AbandonedCartReminder, s.GiftWrapOption);
        });

        admin.MapPut("/settings", async (AppDbContext db, SettingsDto req) =>
        {
            if (req.FreeShippingThreshold < 0 || req.ShippingFee < 0 || req.MinOrderTotal < 0 || req.MaxQuantityPerItem < 1)
                return Results.BadRequest(new ErrorDto("Değerler negatif olamaz; ürün başına en az 1 adet olmalı."));
            var s = await db.Settings.FirstAsync();
            s.FreeShippingThreshold = req.FreeShippingThreshold;
            s.ShippingFee = req.ShippingFee;
            s.MinOrderTotal = req.MinOrderTotal;
            s.MaxQuantityPerItem = req.MaxQuantityPerItem;
            s.ShowFreeShippingBar = req.ShowFreeShippingBar;
            s.AbandonedCartReminder = req.AbandonedCartReminder;
            s.GiftWrapOption = req.GiftWrapOption;
            await db.SaveChangesAsync();
            return Results.Ok(req);
        });
    }

    // ---------------- Yardımcılar ----------------

    private static async Task<List<StockRowDto>> LoadStockRowsAsync(AppDbContext db)
    {
        var variants = await db.Variants.AsNoTracking().Include(v => v.Product).ToListAsync();
        return variants
            .OrderBy(v => v.Product!.Name).ThenBy(v => v.ColorName)
            .ThenBy(v => SizeRank(v.Size))
            .Select(v => ToStockRow(v, v.Product!)).ToList();
    }

    private static int SizeRank(string size) =>
        int.TryParse(size, out var n) ? n : size switch { "XS" => 1, "S" => 2, "M" => 3, "L" => 4, "XL" => 5, "XXL" => 6, _ => 99 };

    private static StockRowDto ToStockRow(ProductVariant v, Product p)
    {
        var status = v.Stock == 0 ? "out" : v.Stock <= p.CriticalThreshold ? "critical" : "ok";
        return new StockRowDto(v.Id, p.Id, p.Name, v.ColorName, v.ColorHex, v.Size, v.Sku, v.Stock, p.CriticalThreshold, status);
    }

    private static OrderSummaryDto ToSummary(Order o) =>
        new(o.Id, o.OrderNo, o.CreatedAt, o.CustomerName, o.Lines.Sum(l => l.Quantity), o.PaymentMethod, o.Total, o.Status, o.PaymentState,
            o.ReturnRequestedAt is not null && o.Status == OrderStatus.Delivered);

    private static OrderDetailDto ToDetail(Order o) =>
        new(o.Id, o.OrderNo, o.CreatedAt, o.Status, o.CustomerName, o.Email, o.Phone, o.Address, o.City,
            o.PaymentMethod, o.Installments, o.Subtotal, o.Discount, o.Shipping, o.Total,
            o.CouponCode, o.TrackingNo, o.Note,
            o.Lines.Select(l => new OrderLineDto(l.ProductName, l.ColorName, l.Size, l.Sku, l.UnitPrice, l.Quantity, l.UnitPrice * l.Quantity)).ToList(),
            o.PaymentState, o.PaymentProvider, o.PaymentId, o.PaidAmount, o.PaidAt, o.PaymentError,
            o.CustomerId is not null, o.DeliveredAt, o.ReturnRequestedAt, o.ReturnReason);

    private static AdminProductDto ToAdminProduct(Product p) =>
        new(p.Id, p.Name, p.Slug, p.Code, p.Description, p.FabricAndCare, p.CategoryId,
            p.Price, p.SalePrice, p.Cost, p.VatRate, p.IsPublished, p.IsNew, p.IsFeatured,
            p.CriticalThreshold, p.LowStockDisplay,
            p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
            p.Variants.OrderBy(v => v.ColorName).ThenBy(v => SizeRank(v.Size))
                .Select(v => new AdminVariantDto(v.Id, v.ColorName, v.ColorHex, v.Size, v.Sku, v.Stock)).ToList());

    private static CouponDto ToCoupon(Coupon c) =>
        new(c.Id, c.Code, c.Type, c.Value, c.MinCartTotal, c.UsageLimit, c.UsedCount, c.StartsAt, c.EndsAt, c.IsActive);

    private static async Task<string?> ApplyProductAsync(AppDbContext db, Product p, AdminProductDto req)
    {
        var reqVariants = req.Variants ?? [];
        var reqImages = req.ImageUrls ?? [];
        if (string.IsNullOrWhiteSpace(req.Name)) return "Ürün adı gerekli.";
        if (string.IsNullOrWhiteSpace(req.Code)) return "Ürün kodu gerekli.";
        if (req.Price <= 0) return "Satış fiyatı 0'dan büyük olmalı.";
        if (req.SalePrice is not null && (req.SalePrice <= 0 || req.SalePrice >= req.Price))
            return "İndirimli fiyat, satış fiyatından düşük olmalı.";
        if (req.Cost < 0) return "Maliyet negatif olamaz.";
        if (!await db.Categories.AnyAsync(c => c.Id == req.CategoryId)) return "Kategori bulunamadı.";

        var code = req.Code.Trim().ToUpperInvariant();
        if (await db.Products.AnyAsync(x => x.Code == code && x.Id != p.Id)) return "Bu ürün kodu başka bir üründe kullanılıyor.";

        var slug = Slugify(string.IsNullOrWhiteSpace(req.Slug) ? req.Name : req.Slug);
        if (await db.Products.AnyAsync(x => x.Slug == slug && x.Id != p.Id)) slug = $"{slug}-{code.ToLowerInvariant()}";

        var dupes = reqVariants.GroupBy(v => ((v.ColorName ?? "").Trim().ToLowerInvariant(), (v.Size ?? "").Trim().ToLowerInvariant())).Any(g => g.Count() > 1);
        if (dupes) return "Aynı renk ve beden iki kez eklenemez.";
        if (reqVariants.Any(v => string.IsNullOrWhiteSpace(v.ColorName) || string.IsNullOrWhiteSpace(v.Size)))
            return "Her varyantın rengi ve bedeni olmalı.";
        if (reqVariants.Any(v => v.Stock < 0)) return "Stok negatif olamaz.";

        p.Name = req.Name.Trim();
        p.Slug = slug;
        p.Code = code;
        p.Description = req.Description ?? "";
        p.FabricAndCare = req.FabricAndCare;
        p.CategoryId = req.CategoryId;
        p.Price = req.Price;
        p.SalePrice = req.SalePrice;
        p.Cost = req.Cost;
        p.VatRate = req.VatRate is > 0 and < 1 ? req.VatRate : 0.10m;
        p.IsPublished = req.IsPublished;
        p.IsNew = req.IsNew;
        p.IsFeatured = req.IsFeatured;
        p.CriticalThreshold = Math.Max(0, req.CriticalThreshold);
        p.LowStockDisplay = Math.Max(0, req.LowStockDisplay);

        // Görseller: gelen listeyle değiştir
        p.Images.Clear();
        var order = 0;
        foreach (var url in reqImages.Where(u => !string.IsNullOrWhiteSpace(u)))
            p.Images.Add(new ProductImage { Url = url.Trim(), Alt = p.Name, SortOrder = order++ });

        // Varyantlar: güncelle / ekle / kaldır; stok farkını hareket olarak kaydet
        var keepIds = reqVariants.Where(v => v.Id is not null).Select(v => v.Id!.Value).ToHashSet();
        foreach (var gone in p.Variants.Where(v => !keepIds.Contains(v.Id)).ToList())
            p.Variants.Remove(gone);

        foreach (var rv in reqVariants)
        {
            var sku = string.IsNullOrWhiteSpace(rv.Sku)
                ? $"{code}-{ColorCode(rv.ColorName)}-{rv.Size.Trim().ToUpperInvariant()}"
                : rv.Sku.Trim().ToUpperInvariant();

            var existing = rv.Id is null ? null : p.Variants.FirstOrDefault(v => v.Id == rv.Id);
            if (existing is null)
            {
                var nv = new ProductVariant { ColorName = rv.ColorName.Trim(), ColorHex = rv.ColorHex, Size = rv.Size.Trim(), Sku = sku, Stock = rv.Stock };
                p.Variants.Add(nv);
                if (rv.Stock > 0)
                    db.StockMovements.Add(new StockMovement { Variant = nv, Delta = rv.Stock, Type = StockMovementType.Restock, Note = "Ürün kartından ilk stok" });
            }
            else
            {
                var delta = rv.Stock - existing.Stock;
                existing.ColorName = rv.ColorName.Trim();
                existing.ColorHex = rv.ColorHex;
                existing.Size = rv.Size.Trim();
                existing.Sku = sku;
                existing.Stock = rv.Stock;
                if (delta != 0)
                    db.StockMovements.Add(new StockMovement { VariantId = existing.Id, Delta = delta, Type = StockMovementType.Adjustment, Note = "Ürün kartından düzeltme" });
            }
        }

        var skus = p.Variants.Select(v => v.Sku).ToList();
        if (skus.Count != skus.Distinct().Count()) return "Aynı SKU iki varyantta kullanılamaz.";
        var ownIds = p.Variants.Where(v => v.Id != 0).Select(v => v.Id).ToList();
        if (await db.Variants.AnyAsync(v => skus.Contains(v.Sku) && !ownIds.Contains(v.Id) && v.ProductId != p.Id))
            return "SKU'lardan biri başka bir üründe kullanılıyor.";

        return null;
    }

    private static async Task<string?> ApplyCouponAsync(AppDbContext db, Coupon c, CouponDto req)
    {
        var code = (req.Code ?? "").Trim().ToUpperInvariant();
        if (code.Length < 3) return "Kod en az 3 karakter olmalı.";
        if (req.Value <= 0) return "İndirim değeri 0'dan büyük olmalı.";
        if (req.Type == DiscountType.Percent && req.Value >= 100) return "Yüzde indirim 100'den küçük olmalı.";
        if (await db.Coupons.AnyAsync(x => x.Code == code && x.Id != c.Id)) return "Bu kod zaten var.";

        c.Code = code;
        c.Type = req.Type;
        c.Value = req.Value;
        c.MinCartTotal = Math.Max(0, req.MinCartTotal);
        c.UsageLimit = req.UsageLimit;
        c.StartsAt = req.StartsAt;
        c.EndsAt = req.EndsAt;
        c.IsActive = req.IsActive;
        return null;
    }

    private static string ColorCode(string color)
    {
        var s = Slugify(color).Replace("-", "");
        return (s.Length >= 2 ? s[..2] : s.PadRight(2, 'X')).ToUpperInvariant();
    }

    public static string Slugify(string text)
    {
        var map = new Dictionary<char, char> { ['ç'] = 'c', ['ğ'] = 'g', ['ı'] = 'i', ['ö'] = 'o', ['ş'] = 's', ['ü'] = 'u' };
        var sb = new StringBuilder();
        foreach (var ch in text.Trim().ToLower(new CultureInfo("tr-TR")))
        {
            var c = map.TryGetValue(ch, out var m) ? m : ch;
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }
}
