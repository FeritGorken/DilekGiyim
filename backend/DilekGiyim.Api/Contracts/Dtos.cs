using DilekGiyim.Api.Domain;

namespace DilekGiyim.Api.Contracts;

public record ErrorDto(string Message);

// ---------- Mağaza: katalog ----------

public record CategoryDto(int Id, string Name, string Slug);

public record ColorDto(string Name, string Hex);

/// <summary>StockStatus: "in" (stokta), "low" (az kaldı), "out" (tükendi)</summary>
public record ProductListItemDto(
    int Id, string Slug, string Name, string CategoryName, string CategorySlug,
    decimal Price, decimal? SalePrice, string? ImageUrl, IReadOnlyList<ColorDto> Colors,
    bool IsNew, string StockStatus, int? LowStockCount);

public record ProductImageDto(string Url, string Alt);

/// <summary>Available, gerçek stok yerine en fazla LowStockDisplay + 1 olarak döner (stok miktarı dışarı sızmasın).</summary>
public record VariantDto(int Id, string ColorName, string ColorHex, string Size, int Available);

public record ProductDetailDto(
    int Id, string Slug, string Code, string Name, string Description, string? FabricAndCare,
    string CategoryName, string CategorySlug, decimal Price, decimal? SalePrice, bool IsNew,
    int LowStockDisplay, IReadOnlyList<ProductImageDto> Images, IReadOnlyList<ColorDto> Colors,
    IReadOnlyList<string> Sizes, IReadOnlyList<VariantDto> Variants);

public record PublicSettingsDto(decimal FreeShippingThreshold, decimal ShippingFee, int MaxQuantityPerItem, bool ShowFreeShippingBar);

// ---------- Mağaza: sepet ve sipariş ----------

public record CartItemRequest(int VariantId, int Quantity);

public record CartQuoteRequest(List<CartItemRequest> Items, string? CouponCode);

public record CartLineDto(
    int VariantId, int ProductId, string Slug, string ProductName, string? ImageUrl,
    string ColorName, string Size, decimal UnitPrice, decimal? OriginalPrice,
    int Quantity, int RequestedQuantity, int MaxQuantity, decimal LineTotal, string? Warning);

public record CartQuoteDto(
    List<CartLineDto> Lines, int ItemCount, decimal Subtotal, decimal Discount, decimal Shipping, decimal Total,
    decimal FreeShippingThreshold, decimal FreeShippingRemaining,
    string? CouponCode, bool CouponApplied, string? CouponMessage);

public record CheckoutRequest(
    string CustomerName, string Email, string Phone, string Address, string City,
    string PaymentMethod, int Installments, List<CartItemRequest> Items, string? CouponCode, string? Note,
    string? IdentityNumber = null);

/// <summary>PaymentPageUrl doluysa müşteri ödeme sayfasına yönlendirilir; PublicKey sonuç sayfası içindir.</summary>
public record CheckoutResultDto(string OrderNo, decimal Total, string? PaymentPageUrl, string PublicKey);

public record OrderPaymentStatusDto(string OrderNo, OrderStatus Status, PaymentState PaymentState, string PaymentMethod, decimal Total, string? Error);

// ---------- Admin ----------

public record DailySalesDto(DateOnly Date, decimal Total);

public record TopProductDto(string Name, int Quantity);

public record OrderSummaryDto(
    int Id, string OrderNo, DateTime CreatedAt, string CustomerName, int ItemCount,
    string PaymentMethod, decimal Total, OrderStatus Status, PaymentState PaymentState);

public record OrderLineDto(string ProductName, string ColorName, string Size, string Sku, decimal UnitPrice, int Quantity, decimal LineTotal);

public record OrderDetailDto(
    int Id, string OrderNo, DateTime CreatedAt, OrderStatus Status,
    string CustomerName, string Email, string Phone, string Address, string City,
    string PaymentMethod, int Installments,
    decimal Subtotal, decimal Discount, decimal Shipping, decimal Total,
    string? CouponCode, string? TrackingNo, string? Note, List<OrderLineDto> Lines,
    PaymentState PaymentState, string? PaymentProvider, string? PaymentId, decimal? PaidAmount, DateTime? PaidAt, string? PaymentError);

public record UpdateOrderStatusRequest(OrderStatus Status, string? TrackingNo, PaymentState? PaymentState = null);

/// <summary>Status: "ok", "critical", "out"</summary>
public record StockRowDto(
    int VariantId, int ProductId, string ProductName, string ColorName, string ColorHex,
    string Size, string Sku, int Stock, int CriticalThreshold, string Status);

public record StockSummaryDto(int ProductCount, int VariantCount, int TotalUnits, int Critical, int Out);

public record StockListDto(StockSummaryDto Summary, List<StockRowDto> Rows);

public record StockAdjustRequest(int Delta, StockMovementType Type, string? Note);

public record StockMovementDto(
    int Id, DateTime CreatedAt, string ProductName, string ColorName, string Size,
    int Delta, StockMovementType Type, string? Reference, string? Note);

public record DashboardDto(
    decimal Revenue, decimal RevenueChangePct, int OrderCount, int PendingOrders, decimal AverageBasket,
    int CriticalCount, int OutOfStockCount,
    List<DailySalesDto> Daily, List<StockRowDto> Critical, List<OrderSummaryDto> RecentOrders, List<TopProductDto> TopProducts);

public record AdminProductListItemDto(
    int Id, string Name, string Code, string CategoryName, decimal Price, decimal? SalePrice,
    int TotalStock, int VariantCount, bool IsPublished, string? ImageUrl);

public record AdminVariantDto(int? Id, string ColorName, string ColorHex, string Size, string Sku, int Stock);

public record AdminProductDto(
    int Id, string Name, string Slug, string Code, string Description, string? FabricAndCare, int CategoryId,
    decimal Price, decimal? SalePrice, decimal Cost, decimal VatRate,
    bool IsPublished, bool IsNew, bool IsFeatured, int CriticalThreshold, int LowStockDisplay,
    List<string> ImageUrls, List<AdminVariantDto> Variants);

public record BulkPriceRequest(int? CategoryId, bool Increase, decimal Percent, bool RoundTo90, bool Apply);

public record BulkPriceRowDto(int ProductId, string Name, decimal Current, decimal New);

public record CouponDto(
    int Id, string Code, DiscountType Type, decimal Value, decimal MinCartTotal,
    int? UsageLimit, int UsedCount, DateTime? StartsAt, DateTime? EndsAt, bool IsActive);

public record CampaignDto(int Id, string Name, string Description, DateTime StartsAt, DateTime EndsAt, bool IsActive);

public record SettingsDto(
    decimal FreeShippingThreshold, decimal ShippingFee, decimal MinOrderTotal, int MaxQuantityPerItem,
    bool ShowFreeShippingBar, bool AbandonedCartReminder, bool GiftWrapOption);
