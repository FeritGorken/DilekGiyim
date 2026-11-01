using System.Globalization;
using DilekGiyim.Api.Domain;
using Microsoft.Extensions.Options;

namespace DilekGiyim.Api.Payments;

/// <summary>
/// Yalnızca geliştirme için sahte ödeme sağlayıcısı: anahtar gerekmez.
/// Müşteri sitedeki "/odeme/test" sayfasına yönlendirilir; orada "Başarılı" veya "Başarısız" seçilir.
/// Canlı ortamda çalışmasına izin verilmez (Program.cs).
/// </summary>
public class MockPaymentProvider(IOptions<PaymentOptions> options) : IPaymentProvider
{
    public string Name => "mock";

    public Task<PaymentInitResult> InitializeAsync(PaymentInitRequest req, CancellationToken ct = default)
    {
        var token = "mock-" + Guid.NewGuid().ToString("N");
        var amount = req.Order.Total.ToString("0.00", CultureInfo.InvariantCulture);
        var url = $"{options.Value.PublicBaseUrl.TrimEnd('/')}/odeme/test?token={token}&amount={amount}&order={req.Order.OrderNo}";
        return Task.FromResult(new PaymentInitResult(true, token, url, null));
    }

    public Task<PaymentVerifyResult> VerifyAsync(Order order, IFormCollection? callbackForm, CancellationToken ct = default)
    {
        // Arka plan temizliğinde (form yok) sonuç bilinmez: zaman aşımına uğramış sayılır
        var result = callbackForm?["mockResult"].ToString();
        PaymentVerifyResult r = result switch
        {
            "success" => new(PaymentOutcome.Paid, "MOCK-" + order.OrderNo, order.Total, 1, null),
            "review" => new(PaymentOutcome.UnderReview, "MOCK-" + order.OrderNo, order.Total, 1, null),
            "fail" => new(PaymentOutcome.Failed, null, null, 1, "Kart reddedildi (test)."),
            _ => new(PaymentOutcome.Failed, null, null, 1, "Ödeme tamamlanmadı.")
        };
        return Task.FromResult(r);
    }
}
