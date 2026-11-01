using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DilekGiyim.Api.Domain;
using Microsoft.Extensions.Options;

namespace DilekGiyim.Api.Payments;

/// <summary>
/// iyzico Ödeme Formu (Checkout Form) entegrasyonu. Resmi SDK yerine REST API doğrudan çağrılır
/// (HMACSHA256 / IYZWSv2 kimlik doğrulama).
/// Akış: Initialize → müşteri iyzico sayfasında öder → iyzico callbackUrl'e "token" POST eder → Retrieve ile sonuç doğrulanır.
/// </summary>
public class IyzicoPaymentProvider(HttpClient http, IOptions<PaymentOptions> options, ILogger<IyzicoPaymentProvider> logger) : IPaymentProvider
{
    private const string InitializePath = "/payment/iyzipos/checkoutform/initialize/auth/ecom";
    private const string RetrievePath = "/payment/iyzipos/checkoutform/auth/ecom/detail";

    /// <summary>T.C. kimlik no girilmezse iyzico'nun kabul ettiği genel değer</summary>
    private const string FallbackIdentityNumber = "11111111111";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly PaymentOptions _opt = options.Value;

    public string Name => "iyzico";

    public async Task<PaymentInitResult> InitializeAsync(PaymentInitRequest req, CancellationToken ct = default)
    {
        var o = req.Order;
        var (name, surname) = SplitName(o.CustomerName);
        var address = new { contactName = o.CustomerName, city = o.City, country = "Turkey", address = o.Address };

        var body = new
        {
            locale = "tr",
            conversationId = o.OrderNo,
            price = Money(o.Total),
            paidPrice = Money(o.Total),
            currency = "TRY",
            basketId = o.OrderNo,
            paymentGroup = "PRODUCT",
            callbackUrl = req.CallbackUrl,
            enabledInstallments = _opt.Installments,
            buyer = new
            {
                id = "G-" + o.OrderNo,
                name,
                surname,
                gsmNumber = NormalizePhone(o.Phone),
                email = o.Email,
                identityNumber = string.IsNullOrWhiteSpace(req.IdentityNumber) ? FallbackIdentityNumber : req.IdentityNumber.Trim(),
                registrationAddress = o.Address,
                city = o.City,
                country = "Turkey",
                ip = req.BuyerIp
            },
            shippingAddress = address,
            billingAddress = address,
            basketItems = BuildBasket(o)
        };

        using var doc = await PostAsync(InitializePath, body, ct);
        if (doc is null) return PaymentInitResult.Fail("Ödeme sistemine ulaşılamadı.");

        var root = doc.RootElement;
        if (Str(root, "status") != "success")
        {
            var msg = Str(root, "errorMessage") ?? "Ödeme başlatılamadı.";
            logger.LogWarning("iyzico initialize hata {Code}: {Message} (sipariş {OrderNo})", Str(root, "errorCode"), msg, o.OrderNo);
            return PaymentInitResult.Fail(msg);
        }

        return new PaymentInitResult(true, Str(root, "token"), Str(root, "paymentPageUrl"), null);
    }

    public async Task<PaymentVerifyResult> VerifyAsync(Order order, IFormCollection? callbackForm, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(order.PaymentToken))
            return new(PaymentOutcome.Failed, null, null, 1, "Ödeme oturumu bulunamadı.");

        using var doc = await PostAsync(RetrievePath, new { locale = "tr", conversationId = order.OrderNo, token = order.PaymentToken }, ct);
        if (doc is null) return new(PaymentOutcome.Pending, null, null, 1, "Ödeme sistemine ulaşılamadı.");

        var root = doc.RootElement;
        var paymentStatus = Str(root, "paymentStatus");

        if (Str(root, "status") != "success")
        {
            // Müşteri formu doldurmadan dönmüş veya oturum hiç tamamlanmamış olabilir
            var msg = Str(root, "errorMessage") ?? "Ödeme tamamlanmadı.";
            return new(PaymentOutcome.Failed, null, null, 1, msg);
        }

        if (paymentStatus is null || paymentStatus == "INIT_THREEDS" || paymentStatus == "CALLBACK_THREEDS")
            return new(PaymentOutcome.Pending, null, null, 1, null);

        if (paymentStatus != "SUCCESS")
            return new(PaymentOutcome.Failed, null, null, 1, Str(root, "errorMessage") ?? "Ödeme reddedildi.");

        // Tutar ve sepet numarası siparişle eşleşmeli
        var basketId = Str(root, "basketId");
        var price = Dec(root, "price");
        if (basketId != order.OrderNo || price is null || Math.Abs(price.Value - order.Total) > 0.01m)
        {
            logger.LogError("iyzico tutar/sepet uyuşmazlığı: sipariş {OrderNo} {Total}, iyzico {BasketId} {Price}", order.OrderNo, order.Total, basketId, price);
            return new(PaymentOutcome.Failed, Str(root, "paymentId"), Dec(root, "paidPrice"), 1, "Ödeme tutarı siparişle eşleşmiyor.");
        }

        var installment = root.TryGetProperty("installment", out var inst) && inst.TryGetInt32(out var n) ? n : 1;
        var fraud = root.TryGetProperty("fraudStatus", out var fs) && fs.TryGetInt32(out var f) ? f : 1;
        var outcome = fraud switch
        {
            1 => PaymentOutcome.Paid,
            0 => PaymentOutcome.UnderReview,
            _ => PaymentOutcome.Failed
        };
        return new(outcome, Str(root, "paymentId"), Dec(root, "paidPrice"), installment,
            outcome == PaymentOutcome.Failed ? "Ödeme güvenlik kontrolünden geçmedi." : null);
    }

    // ---------------- Yardımcılar ----------------

    /// <summary>
    /// Sepet kalemleri: iyzico, kalemlerin toplamının "price" ile birebir eşit olmasını ister.
    /// İndirim ürün satırlarına oransal dağıtılır, kargo ayrı kalem olarak eklenir.
    /// </summary>
    private static List<object> BuildBasket(Order o)
    {
        var lines = o.Lines.Select(l => new { l, gross = l.UnitPrice * l.Quantity }).ToList();
        var productTotal = o.Total - o.Shipping;
        var subtotal = lines.Sum(x => x.gross);

        var amounts = lines.Select(x => subtotal == 0 ? 0 : Math.Round(x.gross * productTotal / subtotal, 2, MidpointRounding.AwayFromZero)).ToList();
        // Yuvarlama farkını en büyük kaleme ekle
        var diff = productTotal - amounts.Sum();
        if (diff != 0 && amounts.Count > 0)
        {
            var i = amounts.IndexOf(amounts.Max());
            amounts[i] += diff;
        }

        var items = new List<object>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (amounts[i] <= 0) continue; // iyzico 0 TL kalem kabul etmez
            var l = lines[i].l;
            items.Add(new
            {
                id = l.Sku,
                name = $"{l.ProductName} ({l.ColorName}, {l.Size}) x{l.Quantity}",
                category1 = "Giyim",
                itemType = "PHYSICAL",
                price = Money(amounts[i])
            });
        }
        if (o.Shipping > 0)
            items.Add(new { id = "KARGO", name = "Kargo", category1 = "Kargo", itemType = "VIRTUAL", price = Money(o.Shipping) });
        return items;
    }

    private async Task<JsonDocument?> PostAsync(string path, object body, CancellationToken ct)
    {
        var iyz = _opt.Iyzico;
        var json = JsonSerializer.Serialize(body, Json);
        var randomKey = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)
                        + RandomNumberGenerator.GetInt32(100000, 999999).ToString(CultureInfo.InvariantCulture);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(iyz.SecretKey));
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(randomKey + path + json))).ToLowerInvariant();
        var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes($"apiKey:{iyz.ApiKey}&randomKey:{randomKey}&signature:{signature}"));

        using var msg = new HttpRequestMessage(HttpMethod.Post, iyz.BaseUrl.TrimEnd('/') + path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        msg.Headers.TryAddWithoutValidation("Authorization", "IYZWSv2 " + authorization);
        msg.Headers.TryAddWithoutValidation("x-iyzi-rnd", randomKey);
        msg.Headers.Accept.ParseAdd("application/json");

        try
        {
            using var res = await http.SendAsync(msg, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            return JsonDocument.Parse(text);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "iyzico isteği başarısız: {Path}", path);
            return null;
        }
    }

    private static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static decimal? Dec(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d) ? d : null;

    private static (string name, string surname) SplitName(string full)
    {
        var parts = full.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1) return (full.Trim(), full.Trim());
        return (string.Join(' ', parts[..^1]), parts[^1]);
    }

    /// <summary>0532 123 45 67 → +905321234567</summary>
    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("90") && digits.Length == 12) return "+" + digits;
        if (digits.StartsWith('0') && digits.Length == 11) return "+9" + digits;
        if (digits.Length == 10) return "+90" + digits;
        return "+" + digits;
    }
}
