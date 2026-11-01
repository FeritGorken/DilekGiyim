using DilekGiyim.Api.Data;
using DilekGiyim.Api.Domain;
using DilekGiyim.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DilekGiyim.Api.Payments;

/// <summary>
/// Müşteri ödeme sayfasını kapatıp dönmezse sipariş "Ödeme bekleniyor"da kalır ve stok ayrılı durur.
/// Bu servis 5 dakikada bir bekleyen siparişleri ödeme kuruluşuna sorar:
/// ödenmişse onaylar, süresi dolmuşsa iptal edip stoğu ve kupon kullanımını geri alır.
/// </summary>
public class PendingPaymentCleanup(IServiceScopeFactory scopes, IOptions<PaymentOptions> options, ILogger<PendingPaymentCleanup> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Bekleyen ödemeler kontrol edilemedi");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var provider = scope.ServiceProvider.GetRequiredService<IPaymentProvider>();
        var orders = scope.ServiceProvider.GetRequiredService<OrderService>();

        var cutoff = DateTime.UtcNow.AddMinutes(-Math.Max(5, options.Value.PendingTimeoutMinutes));
        var stale = await db.Orders.Include(o => o.Lines)
            .Where(o => o.Status == OrderStatus.AwaitingPayment && o.CreatedAt < cutoff)
            .ToListAsync(ct);

        foreach (var order in stale)
        {
            var result = string.IsNullOrEmpty(order.PaymentToken)
                ? new PaymentVerifyResult(PaymentOutcome.Failed, null, null, 1, "Ödeme başlatılamadı.")
                : await provider.VerifyAsync(order, null, ct);

            if (result.Outcome is PaymentOutcome.Pending or PaymentOutcome.Failed)
            {
                await orders.CancelUnpaidAsync(order, "Ödeme süresi doldu.", ct);
                logger.LogInformation("Ödemesi tamamlanmayan sipariş iptal edildi: {OrderNo}", order.OrderNo);
            }
            else
            {
                await orders.ApplyPaymentResultAsync(order, result, ct);
                logger.LogInformation("Geç gelen ödeme onaylandı: {OrderNo}", order.OrderNo);
            }
        }
    }
}
