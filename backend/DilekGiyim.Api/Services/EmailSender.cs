namespace DilekGiyim.Api.Services;

/// <summary>
/// E-posta gönderimi. Şimdilik e-postalar sunucu kaydına (log) yazılır.
/// Gerçek gönderim için (SMTP, SendGrid, Amazon SES…) bu arayüzü uygulayan bir sınıf yazıp Program.cs'te değiştirin.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

public class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        logger.LogInformation("E-POSTA (gönderilmedi, yalnızca kayıt)\nKime: {To}\nKonu: {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
