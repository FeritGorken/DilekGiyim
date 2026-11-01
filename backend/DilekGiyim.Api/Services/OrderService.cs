using DilekGiyim.Api.Contracts;
using DilekGiyim.Api.Data;
using DilekGiyim.Api.Domain;
using DilekGiyim.Api.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DilekGiyim.Api.Services;

public class OrderService(
    AppDbContext db,
    PricingService pricing,
    IPaymentProvider payments,
    IOptions<PaymentOptions> paymentOptions,
    IEmailSender email,
    ILogger<OrderService> logger)
{
    public record PlaceResult(Order? Order, string? Error, string? PaymentPageUrl = null);

    /// <summary>
    /// Siparişi oluşturur ve stoktan düşer. Stok, kupon ve fiyat sunucuda yeniden kontrol edilir.
    /// Kartla ödemede sipariş "Ödeme bekleniyor" durumunda açılır (stok ayrılır) ve ödeme sayfası adresi döner.
    /// </summary>
    public async Task<PlaceResult> PlaceAsync(CheckoutRequest req, string buyerIp, int? customerId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.CustomerName) || string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Phone) || string.IsNullOrWhiteSpace(req.Address) ||
            string.IsNullOrWhiteSpace(req.City))
            return new(null, "Lütfen teslimat bilgilerinin tamamını doldurun.");

        if (!PaymentMethods.All.Contains(req.PaymentMethod))
            return new(null, "Geçersiz ödeme yöntemi.");

        if (req.Items is null || req.Items.Count == 0)
            return new(null, "Sepetiniz boş.");

        var isCard = req.PaymentMethod == PaymentMethods.Card;
        Order order;

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
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

            order = new Order
            {
                OrderNo = await NextOrderNoAsync(ct),
                Status = isCard ? OrderStatus.AwaitingPayment : OrderStatus.New,
                PaymentState = PaymentState.Pending,
                PaymentProvider = isCard ? payments.Name : null,
                CustomerName = req.CustomerName.Trim(),
                Email = req.Email.Trim(),
                Phone = req.Phone.Trim(),
                Address = req.Address.Trim(),
                City = req.City.Trim(),
                PaymentMethod = req.PaymentMethod,
                Installments = 1,
                Note = req.Note,
                Subtotal = quote.Subtotal,
                Discount = quote.Discount,
                Shipping = quote.Shipping,
                Total = quote.Total,
                CouponCode = quote.CouponApplied ? quote.CouponCode : null,
                CustomerId = customerId
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
        }

        if (!isCard)
        {
            await SendConfirmationAsync(order, ct);
            return new(order, null);
        }

        // Kartla ödeme: ödeme oturumunu başlat
        var callbackUrl = paymentOptions.Value.PublicBaseUrl.TrimEnd('/') + "/api/payments/callback";
        PaymentInitResult init;
        try
        {
            init = await payments.InitializeAsync(new PaymentInitRequest(order, buyerIp, callbackUrl, req.IdentityNumber), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ödeme başlatılamadı: sipariş {OrderNo}", order.OrderNo);
            init = PaymentInitResult.Fail("Ödeme sistemine şu an ulaşılamıyor.");
        }

        if (!init.Success || string.IsNullOrEmpty(init.PaymentPageUrl))
        {
            await CancelUnpaidAsync(order, init.Error ?? "Ödeme başlatılamadı.", ct);
            return new(null, init.Error ?? "Ödeme başlatılamadı. Lütfen tekrar deneyin.");
        }

        order.PaymentToken = init.Token;
        await db.SaveChangesAsync(ct);
        return new(order, null, init.PaymentPageUrl);
    }

    /// <summary>
    /// Ödeme kuruluşundan gelen doğrulanmış sonucu siparişe işler. Aynı sonuç iki kez gelirse etkisizdir.
    /// </summary>
    public async Task ApplyPaymentResultAsync(Order order, PaymentVerifyResult r, CancellationToken ct = default)
    {
        if (order.Status != OrderStatus.AwaitingPayment) return; // zaten işlendi

        switch (r.Outcome)
        {
            case PaymentOutcome.Paid:
            case PaymentOutcome.UnderReview:
                order.Status = OrderStatus.New;
                order.PaymentState = r.Outcome == PaymentOutcome.Paid ? PaymentState.Paid : PaymentState.UnderReview;
                order.PaymentId = r.PaymentId;
                order.PaidAmount = r.PaidAmount ?? order.Total;
                order.PaidAt = DateTime.UtcNow;
                order.Installments = Math.Max(1, r.Installment);
                order.PaymentError = null;
                await db.SaveChangesAsync(ct);
                await SendConfirmationAsync(order, ct);
                break;

            case PaymentOutcome.Failed:
                await CancelUnpaidAsync(order, r.Error ?? "Ödeme başarısız.", ct);
                break;

            case PaymentOutcome.Pending:
                break;
        }
    }

    /// <summary>Ödemesi alınamayan siparişi iptal eder: stok ve kupon kullanımı geri alınır.</summary>
    public async Task CancelUnpaidAsync(Order order, string reason, CancellationToken ct = default)
    {
        if (order.Lines.Count == 0)
            await db.Entry(order).Collection(o => o.Lines).LoadAsync(ct);

        await RestockAsync(order, StockMovementType.Cancel, ct);

        if (!string.IsNullOrEmpty(order.CouponCode))
        {
            var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Code == order.CouponCode, ct);
            if (coupon is not null && coupon.UsedCount > 0) coupon.UsedCount--;
        }

        order.Status = OrderStatus.Cancelled;
        order.PaymentState = PaymentState.Failed;
        order.PaymentError = reason;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Yönetim panelinden durum/ödeme güncellemesi. İptal ve iadede ürünler stoğa geri eklenir.
    /// İptal/iade edilmiş sipariş tekrar açılamaz; kart ödemesi tamamlanmamış sipariş ilerletilemez.
    /// </summary>
    public async Task<string?> ChangeStatusAsync(Order order, OrderStatus next, string? trackingNo, PaymentState? paymentState, CancellationToken ct = default)
    {
        var closed = order.Status is OrderStatus.Cancelled or OrderStatus.Returned;
        if (closed && next != order.Status)
            return "İptal veya iade edilmiş bir sipariş yeniden açılamaz.";

        if (next == OrderStatus.AwaitingPayment && order.Status != OrderStatus.AwaitingPayment)
            return "Sipariş elle 'Ödeme bekleniyor' durumuna alınamaz.";

        if (order.Status == OrderStatus.AwaitingPayment)
        {
            if (next == OrderStatus.Cancelled)
            {
                await CancelUnpaidAsync(order, "Yönetici tarafından iptal edildi.", ct);
                return null;
            }
            if (next != OrderStatus.AwaitingPayment)
                return "Kart ödemesi tamamlanmadan sipariş ilerletilemez.";
        }

        if (!closed && next is OrderStatus.Cancelled or OrderStatus.Returned)
            await RestockAsync(order, next == OrderStatus.Cancelled ? StockMovementType.Cancel : StockMovementType.Return, ct);

        if (paymentState is not null && paymentState != order.PaymentState)
        {
            var error = ValidatePaymentChange(order, paymentState.Value);
            if (error is not null) return error;
            order.PaymentState = paymentState.Value;
            if (paymentState == PaymentState.Paid && order.PaidAt is null)
            {
                order.PaidAt = DateTime.UtcNow;
                order.PaidAmount ??= order.Total;
            }
        }

        if (next == OrderStatus.Delivered && order.DeliveredAt is null) order.DeliveredAt = DateTime.UtcNow;
        order.Status = next;
        if (trackingNo is not null) order.TrackingNo = string.IsNullOrWhiteSpace(trackingNo) ? null : trackingNo.Trim();
        await db.SaveChangesAsync(ct);
        return null;
    }

    private static string? ValidatePaymentChange(Order order, PaymentState next)
    {
        var isCard = order.PaymentMethod == PaymentMethods.Card;
        return next switch
        {
            // Kartla ödemede "ödendi" yalnızca ödeme kuruluşunun doğrulamasıyla (veya inceleme onayıyla) olur
            PaymentState.Paid when isCard && order.PaymentState != PaymentState.UnderReview =>
                "Kart ödemeleri ödeme kuruluşu doğrulamasıyla işaretlenir.",
            PaymentState.Refunded when order.PaymentState is not (PaymentState.Paid or PaymentState.UnderReview) =>
                "Yalnızca ödemesi alınmış sipariş iade edildi olarak işaretlenebilir.",
            PaymentState.Failed or PaymentState.UnderReview =>
                "Bu ödeme durumu elle seçilemez.",
            _ => null
        };
    }

    /// <summary>Sipariş onayı e-postası (gönderim hatası siparişi bozmaz)</summary>
    private async Task SendConfirmationAsync(Order order, CancellationToken ct)
    {
        try
        {
            var lines = string.Join("\n", order.Lines.Select(l => $"- {l.ProductName} ({l.ColorName}, {l.Size}) x{l.Quantity}: {l.UnitPrice * l.Quantity:N2} TL"));
            var body = $"Merhaba {order.CustomerName},\n\nSiparişiniz alındı. Sipariş no: #{order.OrderNo}\n\n{lines}\n\n" +
                       $"Toplam: {order.Total:N2} TL\nÖdeme: {order.PaymentMethod}\n\nDilek Giyim";
            await email.SendAsync(order.Email, $"Siparişiniz alındı (#{order.OrderNo})", body, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sipariş onay e-postası gönderilemedi: {OrderNo}", order.OrderNo);
        }
    }

    private async Task RestockAsync(Order order, StockMovementType type, CancellationToken ct)
    {
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

    private async Task<string> NextOrderNoAsync(CancellationToken ct)
    {
        var numbers = await db.Orders.Select(o => o.OrderNo).ToListAsync(ct);
        var max = numbers.Select(n => int.TryParse(n, out var x) ? x : 0).DefaultIfEmpty(10000).Max();
        return (max + 1).ToString();
    }
}
