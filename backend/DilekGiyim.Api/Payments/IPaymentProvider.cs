using DilekGiyim.Api.Domain;

namespace DilekGiyim.Api.Payments;

/// <summary>
/// Kartla ödeme sağlayıcısı. iyzico, PayTR vb. her sağlayıcı bu arayüzü uygular;
/// sipariş ve stok akışı sağlayıcıdan bağımsız kalır.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Siparişe yazılan kısa ad: "iyzico", "mock" …</summary>
    string Name { get; }

    /// <summary>Ödeme oturumu başlatır ve müşterinin yönlendirileceği ödeme sayfası adresini döner.</summary>
    Task<PaymentInitResult> InitializeAsync(PaymentInitRequest request, CancellationToken ct = default);

    /// <summary>
    /// Ödeme sonucunu sağlayıcıdan sorgular. Sonuç her zaman sunucudan sunucuya doğrulanır;
    /// tarayıcıdan gelen form yalnızca hangi işlemin sorgulanacağını söyler.
    /// </summary>
    Task<PaymentVerifyResult> VerifyAsync(Order order, IFormCollection? callbackForm, CancellationToken ct = default);
}

public record PaymentInitRequest(Order Order, string BuyerIp, string CallbackUrl, string? IdentityNumber);

public record PaymentInitResult(bool Success, string? Token, string? PaymentPageUrl, string? Error)
{
    public static PaymentInitResult Fail(string error) => new(false, null, null, error);
}

public enum PaymentOutcome
{
    Paid,
    UnderReview,
    Failed,
    /// <summary>Sağlayıcıda henüz sonuç yok (müşteri ödeme sayfasında)</summary>
    Pending
}

public record PaymentVerifyResult(PaymentOutcome Outcome, string? PaymentId, decimal? PaidAmount, int Installment, string? Error);

public class PaymentOptions
{
    public const string Section = "Payment";

    /// <summary>"Iyzico" veya "Mock" (Mock yalnızca geliştirme ortamında çalışır)</summary>
    public string Provider { get; set; } = "Mock";

    /// <summary>Müşterinin tarayıcısının sitenize ulaştığı adres, örn. https://www.dilekgiyim.com</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:4200";

    /// <summary>Ödeme sayfasında sunulacak taksit seçenekleri</summary>
    public int[] Installments { get; set; } = [1, 2, 3, 6];

    /// <summary>Bu süreden uzun süre ödeme bekleyen siparişler iptal edilip stok geri alınır</summary>
    public int PendingTimeoutMinutes { get; set; } = 40;

    public IyzicoOptions Iyzico { get; set; } = new();
}

public class IyzicoOptions
{
    public string ApiKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    /// <summary>Test: https://sandbox-api.iyzipay.com · Canlı: https://api.iyzipay.com</summary>
    public string BaseUrl { get; set; } = "https://sandbox-api.iyzipay.com";
}
