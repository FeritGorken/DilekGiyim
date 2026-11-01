using DilekGiyim.Api.Contracts;
using DilekGiyim.Api.Data;
using DilekGiyim.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DilekGiyim.Api.Services;

/// <summary>
/// Sepet tutarını her zaman sunucuda hesaplar: fiyat, stok sınırı, kupon ve kargo.
/// Tarayıcıdan gelen fiyatlara asla güvenilmez.
/// </summary>
public class PricingService(AppDbContext db)
{
    public record QuoteResult(CartQuoteDto Quote, Dictionary<int, ProductVariant> Variants, Coupon? Coupon);

    public async Task<QuoteResult> QuoteAsync(IEnumerable<CartItemRequest> items, string? couponCode, CancellationToken ct = default)
    {
        var settings = await db.Settings.AsNoTracking().FirstAsync(ct);

        // Aynı varyant birden fazla gelirse adetleri birleştir
        var requested = items
            .Where(i => i.Quantity > 0)
            .GroupBy(i => i.VariantId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var ids = requested.Keys.ToList();
        var variants = await db.Variants
            .Include(v => v.Product!).ThenInclude(p => p.Images)
            .Where(v => ids.Contains(v.Id) && v.Product!.IsPublished)
            .ToDictionaryAsync(v => v.Id, ct);

        var lines = new List<CartLineDto>();
        foreach (var (variantId, wanted) in requested)
        {
            if (!variants.TryGetValue(variantId, out var v)) continue; // ürün kaldırılmış veya yayında değil

            var p = v.Product!;
            var max = Math.Max(0, Math.Min(v.Stock, settings.MaxQuantityPerItem));
            var qty = Math.Min(wanted, max);
            string? warning = null;
            if (max == 0) warning = "Bu ürün tükendi.";
            else if (qty < wanted) warning = $"Bu üründen en fazla {max} adet alabilirsiniz.";

            var unit = p.EffectivePrice;
            lines.Add(new CartLineDto(
                v.Id, p.Id, p.Slug, p.Name,
                p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                v.ColorName, v.Size, unit,
                unit < p.Price ? p.Price : null,
                qty, wanted, max, unit * qty, warning));
        }

        var subtotal = lines.Sum(l => l.LineTotal);

        // Kupon
        Coupon? coupon = null;
        decimal discount = 0;
        string? couponMessage = null;
        var code = couponCode?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(code))
        {
            coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Code == code, ct);
            var now = DateTime.UtcNow;
            if (coupon is null || !coupon.IsActive)
                couponMessage = "Bu kod geçerli değil.";
            else if ((coupon.StartsAt is not null && coupon.StartsAt > now) || (coupon.EndsAt is not null && coupon.EndsAt < now))
                couponMessage = "Bu kodun kullanım süresi geçerli değil.";
            else if (coupon.UsageLimit is not null && coupon.UsedCount >= coupon.UsageLimit)
                couponMessage = "Bu kodun kullanım limiti doldu.";
            else if (subtotal < coupon.MinCartTotal)
                couponMessage = $"Bu kod {coupon.MinCartTotal:N2} TL ve üzeri sepetlerde geçerli.";
            else
            {
                discount = coupon.Type == DiscountType.Percent
                    ? Math.Round(subtotal * coupon.Value / 100m, 2, MidpointRounding.AwayFromZero)
                    : Math.Min(coupon.Value, subtotal);
                couponMessage = coupon.Type == DiscountType.Percent
                    ? $"{coupon.Code} uygulandı: %{coupon.Value:0.##} indirim"
                    : $"{coupon.Code} uygulandı: {coupon.Value:N2} TL indirim";
            }
            if (discount == 0) coupon = null;
        }

        var afterDiscount = subtotal - discount;
        var shipping = afterDiscount <= 0 || afterDiscount >= settings.FreeShippingThreshold ? 0 : settings.ShippingFee;

        var quote = new CartQuoteDto(
            lines,
            lines.Sum(l => l.Quantity),
            subtotal, discount, shipping, afterDiscount + shipping,
            settings.FreeShippingThreshold,
            Math.Max(0, settings.FreeShippingThreshold - afterDiscount),
            code, coupon is not null, couponMessage);

        return new QuoteResult(quote, variants, coupon);
    }
}
