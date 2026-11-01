using DilekGiyim.Api.Data;
using DilekGiyim.Api.Payments;
using DilekGiyim.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DilekGiyim.Api.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        // Ödeme kuruluşu, müşterinin tarayıcısını bu adrese "token" alanıyla POST eder.
        // Sonuç burada sunucudan sunucuya doğrulanır, ardından müşteri sonuç sayfasına yönlendirilir.
        app.MapPost("/api/payments/callback", async (HttpContext http, AppDbContext db, IPaymentProvider provider,
                OrderService orders, IOptions<PaymentOptions> options, ILoggerFactory loggers) =>
            {
                var log = loggers.CreateLogger("Payments");
                var baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');

                var form = http.Request.HasFormContentType ? await http.Request.ReadFormAsync() : null;
                var token = form?["token"].ToString();
                if (string.IsNullOrEmpty(token))
                    return Results.Redirect($"{baseUrl}/odeme/sonuc?error=token", permanent: false, preserveMethod: false);

                var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.PaymentToken == token);
                if (order is null)
                {
                    log.LogWarning("Bilinmeyen ödeme token'ı ile geri dönüş");
                    return Results.Redirect($"{baseUrl}/odeme/sonuc?error=order");
                }

                var result = await provider.VerifyAsync(order, form);
                log.LogInformation("Ödeme sonucu: sipariş {OrderNo} → {Outcome}", order.OrderNo, result.Outcome);
                await orders.ApplyPaymentResultAsync(order, result);

                // 303 benzeri davranış: tarayıcı sonuç sayfasını GET ile açar
                return Results.Redirect($"{baseUrl}/odeme/sonuc?order={Uri.EscapeDataString(order.OrderNo)}&key={order.PublicKey}");
            })
            .DisableAntiforgery()
            .WithTags("Ödeme");
    }
}
