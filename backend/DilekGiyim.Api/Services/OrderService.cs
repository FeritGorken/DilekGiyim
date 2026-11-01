using DilekGiyim.Api.Contracts;
using DilekGiyim.Api.Data;
using DilekGiyim.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DilekGiyim.Api.Services;

public class OrderService(AppDbContext db, PricingService pricing)
{
    public record PlaceResult(Order? Order, string? Error);

    /// <summary>
    /// Siparişi oluşturur ve stoktan düşer. Stok, kupon ve fiyat sunucuda yeniden kontrol edilir.
    /// </summary>
    public async Task<PlaceResult> PlaceAsync(CheckoutRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.CustomerName) || string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Phone) || string.IsNullOrWhiteSpace(req.Address) ||
            string.IsNullOrWhiteSpace(req.City))
            return new(null, "Lütfen teslimat bilgilerinin tamamını doldurun.");

        if (req.Items is null || req.Items.Count == 0)
            return new(null, "Sepetiniz boş.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var (quote, variants, coupon) = await pricing.QuoteAsync(req.Items, req.CouponCode, ct);

        if (quote.Lines.Count == 0 || quote.ItemCount == 0)
            return new(null, "Sepetinizdeki ürünler artık satışta değil.");

        var changed = quote.Lines.FirstOrDefault(l => l.Quantity < l.RequestedQuantity);
        if (changed is not null)
            return new(null, $"{changed.ProductName} ({changed.ColorName}, {changed.Size}) için stok değişti. Lütfen sepetinizi kontrol edin.");

        if (!string.IsNullOrWhiteSpace(req.CouponCode) && !quote.CouponApplied)
            return new(null, quote.CouponMessage ?? "İndirim kodu uygulanamadı.");

        var settings = await db.Settings.AsNoTracking().FirstAsync(ct);
        if (quote.Total < settings.MinOrderTotal)
            return new(null, $"En az sipariş tutarı {settings.MinOrderTotal:N2} TL.");

        var order = new Order
        {
            OrderNo = await NextOrderNoAsync(ct),
            CustomerName = req.CustomerName.Trim(),
            Email = req.Email.Trim(),
            Phone = req.Phone.Trim(),
            Address = req.Address.Trim(),
            City = req.City.Trim(),
            PaymentMethod = req.PaymentMethod,
            Installments = Math.Clamp(req.Installments, 1, 12),
            Note = req.Note,
            Subtotal = quote.Subtotal,
            Discount = quote.Discount,
            Shipping = quote.Shipping,
            Total = quote.Total,
            CouponCode = quote.CouponApplied ? quote.CouponCode : null
        };

        foreach (var line in quote.Lines.Where(l => l.Quantity > 0))
        {
            var v = variants[line.VariantId];
            if (v.Stock < line.Quantity)
                return new(null, $"{line.ProductName} için yeterli stok kalmadı.");

            v.Stock -= line.Quantity;
            order.Lines.Add(new OrderLine
            {
                VariantId = v.Id,
                ProductId = v.ProductId,
                ProductName = line.ProductName,
                ColorName = v.ColorName,
                Size = v.Size,
                Sku = v.Sku,
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity
            });
            db.StockMovements.Add(new StockMovement
            {
                VariantId = v.Id, Delta = -line.Quantity, Type = StockMovementType.Sale, Reference = order.OrderNo
            });
        }

        if (coupon is not null) coupon.UsedCount++;

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(order, null);
    }

    /// <summary>
    /// Sipariş durumunu değiştirir. İptal ve iadede ürünler stoğa geri eklenir.
    /// İptal/iade edilmiş sipariş tekrar açılamaz.
    /// </summary>
    public async Task<string?> ChangeStatusAsync(Order order, OrderStatus next, string? trackingNo, CancellationToken ct = default)
    {
        var closed = order.Status is OrderStatus.Cancelled or OrderStatus.Returned;
        if (closed && next != order.Status)
            return "İptal veya iade edilmiş bir sipariş yeniden açılamaz.";

        if (!closed && next is OrderStatus.Cancelled or OrderStatus.Returned)
        {
            var type = next == OrderStatus.Cancelled ? StockMovementType.Cancel : StockMovementType.Return;
            var ids = order.Lines.Select(l => l.VariantId).ToList();
            var variants = await db.Variants.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
            foreach (var line in order.Lines)
            {
                if (!variants.TryGetValue(line.VariantId, out var v)) continue; // varyant silinmiş
                v.Stock += line.Quantity;
                db.StockMovements.Add(new StockMovement
                {
                    VariantId = v.Id, Delta = line.Quantity, Type = type, Reference = order.OrderNo
                });
            }
        }

        order.Status = next;
        if (trackingNo is not null) order.TrackingNo = string.IsNullOrWhiteSpace(trackingNo) ? null : trackingNo.Trim();
        await db.SaveChangesAsync(ct);
        return null;
    }

    private async Task<string> NextOrderNoAsync(CancellationToken ct)
    {
        var numbers = await db.Orders.Select(o => o.OrderNo).ToListAsync(ct);
        var max = numbers.Select(n => int.TryParse(n, out var x) ? x : 0).DefaultIfEmpty(10000).Max();
        return (max + 1).ToString();
    }
}
