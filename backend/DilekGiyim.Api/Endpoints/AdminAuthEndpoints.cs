using DilekGiyim.Api.Auth;
using DilekGiyim.Api.Contracts;
using DilekGiyim.Api.Data;
using DilekGiyim.Api.Domain;
using DilekGiyim.Api.Payments;
using DilekGiyim.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DilekGiyim.Api.Endpoints;

/// <summary>Yönetim paneli girişi: kullanıcı adı/e-posta + şifre, çerez tabanlı oturum.</summary>
public static class AdminAuthEndpoints
{
    private const string BadCredentials = "Kullanıcı adı veya şifre hatalı.";

    public static void MapAdminAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/admin/auth").WithTags("Admin girişi");

        auth.MapPost("/login", async (HttpContext http, AppDbContext db, PasswordService passwords, AdminLoginRequest req) =>
        {
            var key = (req.UserName ?? "").Trim().ToLowerInvariant();
            var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.UserName == key || u.Email == key);

            if (user is not null && user.LockoutEnd > DateTime.UtcNow)
                return Results.Json(new ErrorDto("Çok fazla hatalı deneme yapıldı. Hesap 15 dakika kilitlendi; şifrenizi sıfırlayabilirsiniz."), statusCode: 423);

            var ok = passwords.Verify(user?.PasswordHash, req.Password ?? "", out var rehash);
            if (user is null || !ok || !user.IsActive)
            {
                if (user is not null && !ok)
                {
                    user.FailedLoginCount++;
                    if (user.FailedLoginCount >= PasswordService.MaxFailedAttempts)
                    {
                        user.LockoutEnd = DateTime.UtcNow.Add(PasswordService.LockoutDuration);
                        user.FailedLoginCount = 0;
                    }
                    await db.SaveChangesAsync();
                }
                return Results.Json(new ErrorDto(BadCredentials), statusCode: StatusCodes.Status401Unauthorized);
            }

            user.FailedLoginCount = 0;
            user.LockoutEnd = null;
            user.LastLoginAt = DateTime.UtcNow;
            if (rehash is not null) user.PasswordHash = rehash;
            await db.SaveChangesAsync();

            await http.SignInAsync(AuthSetup.AdminScheme,
                AuthSetup.Principal(AuthSetup.AdminScheme, user.Id, user.UserName, user.Email, user.SecurityStamp),
                new AuthenticationProperties
                {
                    // "Açık kalsın": 7 gün; değilse tarayıcı kapanınca biter (en fazla 8 saat)
                    IsPersistent = req.Remember,
                    ExpiresUtc = req.Remember ? DateTimeOffset.UtcNow.AddDays(7) : null
                });

            return Results.Ok(new AdminMeDto(user.Id, user.UserName, user.DisplayName, user.Email));
        }).RequireRateLimiting(AuthSetup.LoginRateLimit);

        auth.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(AuthSetup.AdminScheme);
            return Results.NoContent();
        });

        auth.MapGet("/me", async (HttpContext http, AppDbContext db) =>
        {
            var id = http.User.UserId();
            var u = await db.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return u is null ? Results.Unauthorized() : Results.Ok(new AdminMeDto(u.Id, u.UserName, u.DisplayName, u.Email));
        }).RequireAuthorization(AuthSetup.AdminPolicy);

        auth.MapPost("/change-password", async (HttpContext http, AppDbContext db, PasswordService passwords, ChangePasswordRequest req) =>
        {
            var userId = http.User.UserId();
            var u = await db.AdminUsers.FirstOrDefaultAsync(x => x.Id == userId);
            if (u is null) return Results.Unauthorized();
            if (!passwords.Verify(u.PasswordHash, req.CurrentPassword ?? "", out _))
                return Results.BadRequest(new ErrorDto("Mevcut şifre hatalı."));
            var err = PasswordService.Validate(req.NewPassword);
            if (err is not null) return Results.BadRequest(new ErrorDto(err));

            u.PasswordHash = passwords.Hash(req.NewPassword);
            u.SecurityStamp = Guid.NewGuid().ToString("N"); // diğer oturumları kapat
            await db.SaveChangesAsync();
            await http.SignInAsync(AuthSetup.AdminScheme, AuthSetup.Principal(AuthSetup.AdminScheme, u.Id, u.UserName, u.Email, u.SecurityStamp));
            return Results.Ok(new MessageDto("Şifreniz güncellendi."));
        }).RequireAuthorization(AuthSetup.AdminPolicy);

        auth.MapPost("/forgot", async (AppDbContext db, PasswordService passwords, IEmailSender email,
            IOptions<PaymentOptions> site, ForgotPasswordRequest req) =>
        {
            var key = (req.Email ?? "").Trim().ToLowerInvariant();
            var u = await db.AdminUsers.FirstOrDefaultAsync(x => x.Email == key && x.IsActive);
            if (u is not null)
            {
                var token = await passwords.CreateResetTokenAsync(db, AccountType.Admin, u.Id);
                var link = $"{site.Value.PublicBaseUrl.TrimEnd('/')}/admin/sifre-yenile?token={token}";
                await email.SendAsync(u.Email, "Yönetim paneli şifre sıfırlama",
                    $"Merhaba {u.DisplayName},\n\nŞifrenizi sıfırlamak için bağlantı (30 dakika geçerli):\n{link}\n\nBu isteği siz yapmadıysanız dikkate almayın.");
            }
            // Hesap olsun olmasın aynı yanıt (hangi e-postanın kayıtlı olduğu anlaşılmasın)
            return Results.Ok(new MessageDto("Bu e-posta bir hesaba aitse sıfırlama bağlantısı gönderildi."));
        }).RequireRateLimiting(AuthSetup.LoginRateLimit);

        auth.MapPost("/reset", async (AppDbContext db, PasswordService passwords, ResetPasswordRequest req) =>
        {
            var err = PasswordService.Validate(req.NewPassword);
            if (err is not null) return Results.BadRequest(new ErrorDto(err));
            var id = await passwords.ConsumeResetTokenAsync(db, AccountType.Admin, req.Token);
            var u = id is null ? null : await db.AdminUsers.FirstOrDefaultAsync(x => x.Id == id);
            if (u is null) return Results.BadRequest(new ErrorDto("Bağlantı geçersiz veya süresi dolmuş."));

            u.PasswordHash = passwords.Hash(req.NewPassword);
            u.SecurityStamp = Guid.NewGuid().ToString("N");
            u.FailedLoginCount = 0;
            u.LockoutEnd = null;
            await db.SaveChangesAsync();
            return Results.Ok(new MessageDto("Şifreniz yenilendi. Yeni şifrenizle giriş yapabilirsiniz."));
        }).RequireRateLimiting(AuthSetup.LoginRateLimit);
    }

    /// <summary>
    /// Hiç yönetici yoksa yapılandırmadaki ilk kullanıcıyı oluşturur (Admin:InitialUser, Admin:InitialEmail, Admin:InitialPassword).
    /// Canlıda ilk girişten sonra şifreyi değiştirip bu ayarları kaldırın.
    /// </summary>
    public static async Task EnsureInitialAdminAsync(AppDbContext db, PasswordService passwords, IConfiguration config, ILogger logger)
    {
        if (await db.AdminUsers.AnyAsync()) return;

        var user = config["Admin:InitialUser"];
        var mail = config["Admin:InitialEmail"];
        var pass = config["Admin:InitialPassword"];
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(mail) || string.IsNullOrWhiteSpace(pass))
        {
            logger.LogWarning("Hiç yönetici hesabı yok. Admin:InitialUser / InitialEmail / InitialPassword ayarlayarak ilk hesabı oluşturun.");
            return;
        }
        var err = PasswordService.Validate(pass);
        if (err is not null)
        {
            logger.LogError("İlk yönetici şifresi geçersiz: {Error}", err);
            return;
        }

        db.AdminUsers.Add(new AdminUser
        {
            UserName = user.Trim().ToLowerInvariant(),
            Email = mail.Trim().ToLowerInvariant(),
            DisplayName = user.Trim(),
            PasswordHash = passwords.Hash(pass)
        });
        await db.SaveChangesAsync();
        logger.LogInformation("İlk yönetici hesabı oluşturuldu: {User}", user);
    }
}
