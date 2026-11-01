namespace DilekGiyim.Api.Domain;

public enum OrderStatus
{
    New,
    Preparing,
    Shipped,
    Delivered,
    Cancelled,
    Returned,
    /// <summary>Kartla ödemede: sipariş oluştu, stok ayrıldı, ödeme sonucu bekleniyor</summary>
    AwaitingPayment
}

public static class PaymentMethods
{
    public const string Card = "Kredi kartı";
    public const string BankTransfer = "Havale / EFT";
    public const string CashOnDelivery = "Kapıda ödeme";
    public static readonly string[] All = [Card, BankTransfer, CashOnDelivery];
}

public enum PaymentState
{
    /// <summary>Ödeme henüz alınmadı (kart sonucu, havale veya kapıda ödeme bekleniyor)</summary>
    Pending,
    Paid,
    Failed,
    /// <summary>Ödeme alındı ancak ödeme kuruluşu dolandırıcılık incelemesinde; onaylanmadan kargolamayın</summary>
    UnderReview,
    Refunded
}

public class Order
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.New;

    public string CustomerName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";

    public string PaymentMethod { get; set; } = "";
    public int Installments { get; set; } = 1;

    public PaymentState PaymentState { get; set; } = PaymentState.Pending;
    /// <summary>"iyzico", "mock" … (kartla ödemede)</summary>
    public string? PaymentProvider { get; set; }
    /// <summary>Ödeme kuruluşunun oturum anahtarı (iyzico token)</summary>
    public string? PaymentToken { get; set; }
    /// <summary>Ödeme kuruluşundaki işlem numarası</summary>
    public string? PaymentId { get; set; }
    /// <summary>Müşterinin fiilen ödediği tutar (taksit farkı dahil)</summary>
    public decimal? PaidAmount { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaymentError { get; set; }
    /// <summary>Sonuç sayfası için tahmin edilemez anahtar (sipariş no tek başına yetmesin)</summary>
    public string PublicKey { get; set; } = Guid.NewGuid().ToString("N");

    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal Total { get; set; }
    public string? CouponCode { get; set; }

    public string? TrackingNo { get; set; }
    public string? Note { get; set; }

    public List<OrderLine> Lines { get; set; } = [];
}

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int VariantId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ColorName { get; set; } = "";
    public string Size { get; set; } = "";
    public string Sku { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public enum DiscountType
{
    Percent,
    Amount
}

public class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public DiscountType Type { get; set; }
    /// <summary>Percent için 10 = %10; Amount için TL</summary>
    public decimal Value { get; set; }
    public decimal MinCartTotal { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Kampanya kaydı (yönetim panelinde listelenir). Ürün indirimleri ürünün SalePrice alanıyla uygulanır.</summary>
public class Campaign
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum StockMovementType
{
    Sale,
    Restock,
    Return,
    Cancel,
    Adjustment
}

public class StockMovement
{
    public int Id { get; set; }
    public int VariantId { get; set; }
    public ProductVariant? Variant { get; set; }
    public int Delta { get; set; }
    public StockMovementType Type { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Sepet ve kargo kuralları. Tek satır (Id = 1).</summary>
public class StoreSettings
{
    public int Id { get; set; } = 1;
    public decimal FreeShippingThreshold { get; set; } = 3000m;
    public decimal ShippingFee { get; set; } = 89.90m;
    public decimal MinOrderTotal { get; set; }
    public int MaxQuantityPerItem { get; set; } = 9;
    public bool ShowFreeShippingBar { get; set; } = true;
    public bool AbandonedCartReminder { get; set; } = true;
    public bool GiftWrapOption { get; set; }
}
