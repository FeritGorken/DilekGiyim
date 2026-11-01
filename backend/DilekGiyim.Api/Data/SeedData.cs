using DilekGiyim.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DilekGiyim.Api.Data;

/// <summary>
/// Geliştirme için örnek veri. Veritabanı boşsa bir kez çalışır.
/// Trençkot dışındaki ürünler, fiyatlar ve siparişler örnektir.
/// </summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        if (await db.Products.AnyAsync()) return;

        var outerwear = new Category { Name = "Dış Giyim", Slug = "dis-giyim", SortOrder = 1 };
        var dresses = new Category { Name = "Elbise", Slug = "elbise", SortOrder = 2 };
        var shirts = new Category { Name = "Gömlek & Bluz", Slug = "gomlek-bluz", SortOrder = 3 };
        var trousers = new Category { Name = "Pantolon", Slug = "pantolon", SortOrder = 4 };
        var knit = new Category { Name = "Triko", Slug = "triko", SortOrder = 5 };
        db.Categories.AddRange(outerwear, dresses, shirts, trousers, knit);

        var trench = new Product
        {
            Name = "Kemerli Kruvaze Trençkot",
            Slug = "kemerli-kruvaze-trenckot",
            Code = "DG-TR-2601",
            Description = "Geniş yakalı, kruvaze kapanışlı uzun trençkot. Belde ayarlanabilir kuşak, katlamalı manşetler ve omuz detayı. Yumuşak, mat dokulu kumaşıyla gün boyu rahat.",
            Category = outerwear,
            Price = 3490m,
            Cost = 1450m,
            IsNew = true,
            IsFeatured = true,
            CriticalThreshold = 1,
            Images =
            [
                new() { Url = "/images/trench-full.jpg", Alt = "Kemerli kruvaze trençkot, önden görünüm", SortOrder = 0 },
                new() { Url = "/images/trench-collar.jpg", Alt = "Yaka detayı", SortOrder = 1 },
                new() { Url = "/images/trench-belt.jpg", Alt = "Kruvaze düğme ve kuşak detayı", SortOrder = 2 },
                new() { Url = "/images/trench-cuff.jpg", Alt = "Manşet detayı", SortOrder = 3 }
            ]
        };
        AddVariants(trench, "HK", "Haki gri", "#7E8780", new() { ["34"] = 0, ["36"] = 3, ["38"] = 5, ["40"] = 2, ["42"] = 1, ["44"] = 2 });
        AddVariants(trench, "AN", "Antrasit", "#2E2F31", new() { ["34"] = 2, ["36"] = 4, ["38"] = 4, ["40"] = 6, ["42"] = 3, ["44"] = 1 });

        var coat = new Product
        {
            Name = "Yün Karışımlı Uzun Kaban",
            Slug = "yun-karisimli-uzun-kaban",
            Code = "DG-KB-2604",
            Description = "Yün karışımlı, düz kesim uzun kaban.",
            Category = outerwear,
            Price = 5290m,
            SalePrice = 4290m,
            Cost = 1900m,
            IsFeatured = true,
            CriticalThreshold = 2
        };
        AddVariants(coat, "BJ", "Bej", "#C9B79C", new() { ["36"] = 6, ["38"] = 4, ["40"] = 5 });
        AddVariants(coat, "KH", "Kahve", "#4A3B30", new() { ["38"] = 3, ["40"] = 4 });

        var shirt = new Product
        {
            Name = "Saten Gömlek",
            Slug = "saten-gomlek",
            Code = "DG-GM-2611",
            Description = "Akışkan saten kumaştan, rahat kesim gömlek.",
            Category = shirts,
            Price = 1290m,
            Cost = 480m,
            IsFeatured = true,
            CriticalThreshold = 2
        };
        AddVariants(shirt, "EK", "Ekru", "#F1EADF", new() { ["36"] = 2, ["38"] = 7, ["40"] = 5 });
        AddVariants(shirt, "SY", "Siyah", "#1E1A17", new() { ["36"] = 4, ["38"] = 6 });

        var palazzo = new Product
        {
            Name = "Yüksek Bel Palazzo Pantolon",
            Slug = "yuksek-bel-palazzo-pantolon",
            Code = "DG-PN-2607",
            Description = "Yüksek bel, geniş paça palazzo pantolon.",
            Category = trousers,
            Price = 1590m,
            Cost = 560m,
            IsFeatured = true,
            CriticalThreshold = 2
        };
        AddVariants(palazzo, "AN", "Antrasit", "#2E2F31", new() { ["36"] = 3, ["38"] = 0, ["40"] = 4 });

        db.Products.AddRange(trench, coat, shirt, palazzo);

        db.Coupons.AddRange(
            new Coupon { Code = "DILEK10", Type = DiscountType.Percent, Value = 10, UsageLimit = 200, UsedCount = 48 },
            new Coupon { Code = "HOSGELDIN", Type = DiscountType.Amount, Value = 250, MinCartTotal = 1500, UsageLimit = 500, UsedCount = 112 },
            new Coupon { Code = "SEPET15", Type = DiscountType.Percent, Value = 15, MinCartTotal = 5000, UsageLimit = 50, UsedCount = 9 });

        var now = DateTime.UtcNow;
        db.Campaigns.AddRange(
            new Campaign { Name = "Kaban sezonu", Description = "Yün Karışımlı Uzun Kaban üzerinde %19 indirim", StartsAt = now.AddDays(-12), EndsAt = now.AddDays(14) },
            new Campaign { Name = "Sepette %10", Description = "DILEK10 koduyla tüm ürünlerde %10", StartsAt = now.AddDays(-1), EndsAt = now.AddDays(29) },
            new Campaign { Name = "Triko haftası", Description = "Triko kategorisinde 3 al 2 öde", StartsAt = now.AddDays(15), EndsAt = now.AddDays(22) });

        db.Settings.Add(new StoreSettings());

        await db.SaveChangesAsync();

        // Örnek siparişler (stok yukarıdaki güncel değerleri yansıtır, bu yüzden stoktan tekrar düşülmez)
        ProductVariant V(Product p, string color, string size) => p.Variants.First(v => v.ColorName == color && v.Size == size);
        var samples = new (int no, double daysAgo, OrderStatus st, string pay, (Product p, ProductVariant v, int q)[] lines)[]
        {
            (10471, 3.2, OrderStatus.Returned, "Kredi kartı", new[] { (shirt, V(shirt, "Siyah", "36"), 1) }),
            (10474, 2.1, OrderStatus.Delivered, "Kredi kartı", new[] { (shirt, V(shirt, "Ekru", "40"), 1) }),
            (10476, 2.0, OrderStatus.Delivered, "Kredi kartı · 6 taksit", new[] { (trench, V(trench, "Antrasit", "38"), 2) }),
            (10477, 1.4, OrderStatus.Shipped, "Kredi kartı", new[] { (trench, V(trench, "Haki gri", "36"), 1), (shirt, V(shirt, "Siyah", "38"), 2) }),
            (10479, 0.9, OrderStatus.Shipped, "Kredi kartı", new[] { (palazzo, V(palazzo, "Antrasit", "36"), 1) }),
            (10480, 0.2, OrderStatus.New, "Havale / EFT", new[] { (coat, V(coat, "Bej", "38"), 1) }),
            (10481, 0.15, OrderStatus.Preparing, "Kredi kartı · 3 taksit", new[] { (trench, V(trench, "Haki gri", "38"), 1), (shirt, V(shirt, "Ekru", "36"), 1) }),
            (10482, 0.05, OrderStatus.New, "Kredi kartı", new[] { (trench, V(trench, "Antrasit", "40"), 1) })
        };

        var settings = new StoreSettings();
        foreach (var s in samples)
        {
            var order = new Order
            {
                OrderNo = s.no.ToString(),
                CreatedAt = now.AddDays(-s.daysAgo),
                Status = s.st,
                CustomerName = "Örnek Müşteri",
                Email = "musteri@example.com",
                Phone = "05xx xxx xx xx",
                Address = "Örnek adres",
                City = "İstanbul",
                PaymentMethod = s.pay.StartsWith(PaymentMethods.Card) ? PaymentMethods.Card : s.pay,
                Installments = s.pay.Contains("6 taksit") ? 6 : s.pay.Contains("3 taksit") ? 3 : 1,
                PaymentState = s.pay.StartsWith(PaymentMethods.Card) || s.st is OrderStatus.Shipped or OrderStatus.Delivered
                    ? PaymentState.Paid : PaymentState.Pending,
                PaymentProvider = s.pay.StartsWith(PaymentMethods.Card) ? "iyzico" : null
            };
            if (order.PaymentState == PaymentState.Paid) order.PaidAt = order.CreatedAt;
            foreach (var (p, v, q) in s.lines)
            {
                order.Lines.Add(new OrderLine
                {
                    VariantId = v.Id, ProductId = p.Id, ProductName = p.Name,
                    ColorName = v.ColorName, Size = v.Size, Sku = v.Sku,
                    UnitPrice = p.EffectivePrice, Quantity = q
                });
            }
            order.Subtotal = order.Lines.Sum(l => l.UnitPrice * l.Quantity);
            order.Shipping = order.Subtotal >= settings.FreeShippingThreshold ? 0 : settings.ShippingFee;
            order.Total = order.Subtotal + order.Shipping;
            db.Orders.Add(order);
        }

        await db.SaveChangesAsync();
    }

    private static void AddVariants(Product p, string colorCode, string colorName, string hex, Dictionary<string, int> stockBySize)
    {
        foreach (var (size, stock) in stockBySize)
        {
            p.Variants.Add(new ProductVariant
            {
                ColorName = colorName,
                ColorHex = hex,
                Size = size,
                Sku = $"{p.Code}-{colorCode}-{size}",
                Stock = stock
            });
        }
    }
}
