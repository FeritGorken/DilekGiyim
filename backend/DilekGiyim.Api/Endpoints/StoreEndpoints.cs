using DilekGiyim.Api.Auth;
using DilekGiyim.Api.Contracts;
using DilekGiyim.Api.Data;
using DilekGiyim.Api.Domain;
using DilekGiyim.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace DilekGiyim.Api.Endpoints;

/// <summary>Müşterinin kullandığı uç noktalar: katalog, sepet hesabı, sipariş.</summary>
public static class StoreEndpoints
{
    private static readonly string[] SizeOrder = ["XS", "S", "M", "L", "XL", "XXL", "34", "36", "38", "40", "42", "44", "46", "48"];

    public static void MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Mağaza");

        api.MapGet("/categories", async (AppDbContext db) =>
            await db.Categories.OrderBy(c => c.SortOrder)
                .Select(c => new CategoryDto(c.Id, c.Name, c.Slug))
                .ToListAsync());

        api.MapGet("/settings", async (AppDbContext db) =>
        {
            var s = await db.Settings.AsNoTracking().FirstAsync();
            return new PublicSettingsDto(s.FreeShippingThreshold, s.ShippingFee, s.MaxQuantityPerItem, s.ShowFreeShippingBar);
        });

        // ?category=dis-giyim&featured=true&tab=new|sale|best
        api.MapGet("/products", async (AppDbContext db, string? category, bool? featured, string? tab, string? q) =>
        {
            var query = db.Products.AsNoTracking()
                .Include(p => p.Category).Include(p => p.Images).Include(p => p.Variants)
                .Where(p => p.IsPublished);

            if (!string.IsNullOrWhiteSpace(category)) query = query.Where(p => p.Category!.Slug == category);
            if (featured == true) query = query.Where(p => p.IsFeatured);
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(p => p.Name.Contains(q));

            var products = await query.ToListAsync();

            if (tab == "new") products = products.OrderByDescending(p => p.IsNew).ThenByDescending(p => p.CreatedAt).ToList();
            else if (tab == "sale") products = products.Where(p => p.SalePrice is > 0 && p.SalePrice < p.Price).ToList();
            else if (tab == "best")
            {
                var sold = await db.OrderLines
                    .GroupBy(l => l.ProductId)
                    .Select(g => new { g.Key, Qty = g.Sum(l => l.Quantity) })
                    .ToDictionaryAsync(x => x.Key, x => x.Qty);
                products = products.OrderByDescending(p => sold.GetValueOrDefault(p.Id)).ToList();
            }

            return products.Select(ToListItem).ToList();
        });

        api.MapGet("/products/{slug}", async (AppDbContext db, string slug) =>
        {
            var p = await db.Products.AsNoTracking()
                .Include(x => x.Category).Include(x => x.Images).Include(x => x.Variants)
                .FirstOrDefaultAsync(x => x.Slug == slug && x.IsPublished);
            if (p is null) return Results.NotFound(new ErrorDto("Ürün bulunamadı."));

            var cap = p.LowStockDisplay + 1;
            return Results.Ok(new ProductDetailDto(
                p.Id, p.Slug, p.Code, p.Name, p.Description, p.FabricAndCare,
                p.Category!.Name, p.Category.Slug, p.Price, ActiveSale(p), p.IsNew, p.LowStockDisplay,
                p.Images.OrderBy(i => i.SortOrder).Select(i => new ProductImageDto(i.Url, i.Alt)).ToList(),
                Colors(p),
                SortSizes(p.Variants.Select(v => v.Size).Distinct()),
                p.Variants.Select(v => new VariantDto(v.Id, v.ColorName, v.ColorHex, v.Size, Math.Min(v.Stock, cap))).ToList()));
        });

        api.MapPost("/cart/quote", async (PricingService pricing, CartQuoteRequest req) =>
            (await pricing.QuoteAsync(req.Items ?? [], req.CouponCode)).Quote);

        api.MapPost("/orders", async (OrderService orders, CheckoutRequest req, HttpContext http) =>
        {
            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            // Müşteri giriş yapmışsa sipariş hesabına bağlanır (misafir sipariş de mümkün)
            var result = await orders.PlaceAsync(req, ip, http.User.UserId());
            return result.Order is null
                ? Results.BadRequest(new ErrorDto(result.Error!))
                : Results.Ok(new CheckoutResultDto(result.Order.OrderNo, result.Order.Total, result.PaymentPageUrl, result.Order.PublicKey));
        });

        // Üye olmadan sipariş takibi: sipariş no + e-posta
        api.MapGet("/orders/track", async (AppDbContext db, string orderNo, string email) =>
        {
            var no = (orderNo ?? "").Trim().TrimStart('#');
            var mail = (email ?? "").Trim().ToLowerInvariant();
            var o = await db.Orders.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.OrderNo == no);
            if (o is null || o.Email.Trim().ToLowerInvariant() != mail)
                return Results.NotFound(new ErrorDto("Sipariş no ve e-posta eşleşen bir sipariş bulunamadı."));
            return Results.Ok(new TrackOrderDto(o.OrderNo, o.CreatedAt, o.Status, o.PaymentState, o.TrackingNo, o.Total, o.Lines.Sum(l => l.Quantity)));
        }).RequireRateLimiting(AuthSetup.LoginRateLimit);

        // Sonuç sayfası için: sipariş no + gizli anahtar ile ödeme durumu
        api.MapGet("/orders/{orderNo}/status", async (AppDbContext db, string orderNo, string key) =>
        {
            var o = await db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.OrderNo == orderNo);
            if (o is null || !string.Equals(o.PublicKey, key, StringComparison.Ordinal))
                return Results.NotFound(new ErrorDto("Sipariş bulunamadı."));
            return Results.Ok(new OrderPaymentStatusDto(o.OrderNo, o.Status, o.PaymentState, o.PaymentMethod, o.Total, o.PaymentError));
        });
    }

    public static decimal? ActiveSale(Product p) => p.SalePrice is > 0 && p.SalePrice < p.Price ? p.SalePrice : null;

    public static List<ColorDto> Colors(Product p) =>
        p.Variants.GroupBy(v => v.ColorName).Select(g => new ColorDto(g.Key, g.First().ColorHex)).ToList();

    public static List<string> SortSizes(IEnumerable<string> sizes) =>
        sizes.OrderBy(s => Array.IndexOf(SizeOrder, s) is var i && i >= 0 ? i : int.MaxValue).ThenBy(s => s).ToList();

    public static ProductListItemDto ToListItem(Product p)
    {
        var total = p.Variants.Sum(v => v.Stock);
        var status = total == 0 ? "out" : total <= p.LowStockDisplay ? "low" : "in";
        return new ProductListItemDto(
            p.Id, p.Slug, p.Name, p.Category!.Name, p.Category.Slug,
            p.Price, ActiveSale(p),
            p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
            Colors(p), p.IsNew, status, status == "low" ? total : null);
    }
}
