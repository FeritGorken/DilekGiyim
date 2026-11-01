using DilekGiyim.Api.Domain;

namespace DilekGiyim.Api.Contracts;

// ---------- Ortak ----------

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
public record MessageDto(string Message);

// ---------- Admin ----------

public record AdminLoginRequest(string UserName, string Password, bool Remember);
public record AdminMeDto(int Id, string UserName, string DisplayName, string Email);

// ---------- Müşteri ----------

public record RegisterRequest(
    string FirstName, string LastName, string Email, string Phone, string Password,
    bool AcceptTerms, bool MarketingConsent);

public record LoginRequest(string Email, string Password, bool Remember);

public record CustomerMeDto(int Id, string FirstName, string LastName, string Email, string Phone, bool MarketingConsent);

public record UpdateProfileRequest(string FirstName, string LastName, string Email, string Phone, bool MarketingConsent);

public record AddressDto(int Id, string Title, string FullName, string Phone, string City, string District, string Line, bool IsDefault);

public record CustomerOrderLineDto(string ProductName, string? ProductSlug, string? ImageUrl, string ColorName, string Size, int Quantity, decimal LineTotal);

public record CustomerOrderDto(
    int Id, string OrderNo, DateTime CreatedAt, OrderStatus Status, PaymentState PaymentState,
    string PaymentMethod, int Installments, decimal Subtotal, decimal Discount, decimal Shipping, decimal Total,
    string? TrackingNo, DateTime? DeliveredAt, DateTime? ReturnRequestedAt, DateTime? ReturnDeadline,
    bool CanCancel, bool CanRequestReturn, List<CustomerOrderLineDto> Lines);

public record ReturnRequest(string Reason);

/// <summary>Üye olmadan sipariş takibi (sipariş no + e-posta)</summary>
public record TrackOrderDto(string OrderNo, DateTime CreatedAt, OrderStatus Status, PaymentState PaymentState, string? TrackingNo, decimal Total, int ItemCount);
