using System.Security.Claims;
using System.Threading.RateLimiting;
using DilekGiyim.Api.Contracts;
using DilekGiyim.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DilekGiyim.Api.Auth;

/// <summary>
/// Kimlik doğrulama: admin ve müşteri için ayrı çerezler.
/// Çerezler HttpOnly (JavaScript okuyamaz) ve SameSite=Strict (başka sitelerden gönderilmez).
/// </summary>
public static class AuthSetup
{
    public const string AdminScheme = "AdminCookie";
    public const string CustomerScheme = "CustomerCookie";
    public const string AdminPolicy = "Admin";
    public const string CustomerPolicy = "Customer";
    public const string StampClaim = "stamp";
    public const string LoginRateLimit = "login";

    public static IServiceCollection AddDgAuth(this IServiceCollection services, IHostEnvironment env)
    {
        services.AddAuthentication(CustomerScheme)
            .AddCookie(AdminScheme, o =>
            {
                Configure(o, "dg_admin", env);
                o.ExpireTimeSpan = TimeSpan.FromHours(8);
                o.Events.OnValidatePrincipal = ctx => ValidateStampAsync(ctx, admin: true);
            })
            .AddCookie(CustomerScheme, o =>
            {
                Configure(o, "dg_session", env);
                o.ExpireTimeSpan = TimeSpan.FromDays(30);
                o.Events.OnValidatePrincipal = ctx => ValidateStampAsync(ctx, admin: false);
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, p => p.AddAuthenticationSchemes(AdminScheme).RequireAuthenticatedUser())
            .AddPolicy(CustomerPolicy, p => p.AddAuthenticationSchemes(CustomerScheme).RequireAuthenticatedUser());

        // Giriş, üyelik ve şifre sıfırlama uç noktalarında IP başına dakikada 10 istek
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            o.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.ContentType = "application/json";
                await ctx.HttpContext.Response.WriteAsJsonAsync(new ErrorDto("Çok fazla deneme yapıldı. Lütfen bir dakika sonra tekrar deneyin."), ct);
            };
        });

        services.AddSingleton<PasswordService>();
        return services;
    }

    private static void Configure(CookieAuthenticationOptions o, string name, IHostEnvironment env)
    {
        o.Cookie.Name = name;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        // Geliştirmede http, canlıda yalnızca https
        o.Cookie.SecurePolicy = env.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.SlidingExpiration = true;
        // API: giriş sayfasına yönlendirmek yerine 401/403 dön
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    }

    /// <summary>Şifre değiştiğinde veya hesap pasifleştiğinde eski oturumları geçersiz kılar.</summary>
    private static async Task ValidateStampAsync(CookieValidatePrincipalContext ctx, bool admin)
    {
        var id = ctx.Principal?.UserId();
        var stamp = ctx.Principal?.FindFirstValue(StampClaim);
        if (id is null || stamp is null)
        {
            ctx.RejectPrincipal();
            return;
        }

        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var valid = admin
            ? await db.AdminUsers.AnyAsync(u => u.Id == id && u.IsActive && u.SecurityStamp == stamp)
            : await db.Customers.AnyAsync(u => u.Id == id && u.SecurityStamp == stamp);

        if (!valid)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(admin ? AdminScheme : CustomerScheme);
        }
    }

    public static int? UserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static ClaimsPrincipal Principal(string scheme, int id, string name, string email, string stamp) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Email, email),
            new Claim(StampClaim, stamp)
        ], scheme));
}
