using System.Net.Mail;
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

/// <summary>Müşteri hesabı: üyelik, giriş, profil, adresler, favoriler, siparişler.</summary>
public static class AccountEndpoints
{
    public static readonly TimeSpan ReturnWindow = TimeSpan.FromDays(14);

    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var pub = app.MapGroup("/api/account").WithTags("Müşteri hesabı");
        var acc = app.MapGroup("/api/account").WithTags("Müşteri hesabı").RequireAuthorization(AuthSetup.CustomerPolicy);

        // ---------- Üyelik ve giriş ----------

        pub.MapPost("/register", async (HttpContext http, AppDbContext db, PasswordService passwords, RegisterRequest req) =>
        {
            var first = (req.FirstName ?? "").Trim();
            var last = (req.LastName ?? "").Trim();
            var email = NormalizeEmail(req.Email);
            if (first.Length == 0 || last.Length == 0) return Bad("Ad ve soyad gerekli.");
            if (email is null) return Bad("Geçerli bir e-posta adresi girin.");
            if (string.IsNullOrWhiteSpace(req.Phone)) return Bad("Cep telefonu gerekli.");
            if (!req.AcceptTerms) return Bad("Üyelik sözleşmesini ve KVKK aydınlatma metnini onaylamanız gerekiyor.");
            var err = PasswordService.Validate(req.Password);
            if (err is not null) return Bad(err);
            if (await db.Customers.AnyAsync(c => c.Email == email))
                return Results.Conflict(new ErrorDto("Bu e-posta ile kayıtlı bir hesap var. Giriş yapın veya şifrenizi sıfırlayın."));

            var c = new Customer
            {
                FirstName = first, LastName = last, Email = email, Phone = req.Phone.Trim(),
                PasswordHash = passwords.Hash(req.Password),
                MarketingConsent = req.MarketingConsent,
                KvkkAcceptedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            db.Customers.Add(c);
            await db.SaveChangesAsync();
            await SignInAsync(http, c, persistent: false);
            return Results.Ok(ToMe(c));
        }).RequireRateLimiting(AuthSetup.LoginRateLimit);

        pub.MapPost("/login", async (HttpContext http, AppDbContext db, PasswordService passwords, LoginRequest req) =>
        {
            var email = NormalizeEmail(req.Email) ?? "";
            var c = await db.Customers.FirstOrDefaultAsync(x => x.Email == email);
            if (c is not null && c.LockoutEnd > DateTime.UtcNow)
                return Results.Json(new ErrorDto("Çok fazla hatalı deneme yapıldı. 15 dakika sonra tekrar deneyin veya şifrenizi sıfırlayın."), statusCode: 423);

            var ok = passwords.Verify(c?.PasswordHash, req.Password ?? "", out var rehash);
            if (c is null || !ok)
            {
                if (c is not null)
                {
                    c.FailedLoginCount++;
                    if (c.FailedLoginCount >= PasswordService.MaxFailedAttempts)
                    {
                        c.LockoutEnd = DateTime.UtcNow.Add(PasswordService.LockoutDuration);
                        c.FailedLoginCount = 0;
                    }
                    await db.SaveChangesAsync();
                }
                return Results.Json(new ErrorDto("E-posta veya şifre hatalı."), statusCode: StatusCodes.Status401Unauthorized);
            }

            c.FailedLoginCount = 0;
            c.LockoutEnd = null;
            c.LastLoginAt = DateTime.UtcNow;
            if (rehash is not null) c.PasswordHash = rehash;
            await db.SaveChangesAsync();
            await SignInAsync(http, c, req.Remember);
            return Results.Ok(ToMe(c));
        }).RequireRateLimiting(AuthSetup.LoginRateLimit);

        pub.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(AuthSetup.CustomerScheme);
            return Results.NoContent();
        });

        pub.MapPost("/forgot", async (AppDbContext db, PasswordService passwords, IEmailSender mail,
            IOptions<PaymentOptions> site, ForgotPasswordRequest req) =>
        {
            var email = NormalizeEmail(req.Email);
            var c = email is null ? null : await db.Customers.FirstOrDefaultAsync(x => x.Email == email);
            if (c is not null)
            {
                var token = await passwords.CreateResetTokenAsync(db, AccountType.Customer, c.Id);
                var link = $"{site.Value.PublicBaseUrl.TrimEnd('/')}/hesap/sifre-yenile?token={token}";
                await mail.SendAsync(c.Email, "Dilek Giyim şifre yenileme",
                    $"Merhaba {c.FirstName},\n\nŞifrenizi yenilemek için bağlantı (30 dakika geçerli):\n{link}\n\nBu isteği siz yapmadıysanız dikkate almayın.");
            }
            return Results.Ok(new MessageDto("Bu e-posta bir hesaba aitse şifre yenileme bağlantısı gönderildi."));
        }).RequireRateLimiting(AuthSetup.LoginRateLimit);

        pub.MapPost("/reset", async (AppDbContext db, PasswordService passwords, ResetPasswordRequest req) =>
        {
            var err = PasswordService.Validate(req.NewPassword);
            if (err is not null) return Bad(err);
            var id = await passwords.ConsumeResetTokenAsync(db, AccountType.Customer, req.Token);
            var c = id is null ? null : await db.Customers.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Bad("Bağlantı geçersiz veya süresi dolmuş.");
            c.PasswordHash = passwords.Hash(req.NewPassword);
            c.SecurityStamp = Guid.NewGuid().ToString("N");
            c.FailedLoginCount = 0;
            c.LockoutEnd = null;
            await db.SaveChangesAsync();
            return Results.Ok(new MessageDto("Şifreniz yenilendi. Yeni şifrenizle giriş yapabilirsiniz."));
        }).RequireRateLimiting(AuthSetup.LoginRateLimit);

        // ---------- Profil ----------

        acc.MapGet("/me", async (HttpContext http, AppDbContext db) =>
        {
            var c = await Current(http, db);
            return c is null ? Results.Unauthorized() : Results.Ok(ToMe(c));
        });

        acc.MapPut("/me", async (HttpContext http, AppDbContext db, UpdateProfileRequest req) =>
        {
            var c = await Current(http, db);
            if (c is null) return Results.Unauthorized();
            var email = NormalizeEmail(req.Email);
            if (email is null) return Bad("Geçerli bir e-posta adresi girin.");
            if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName)) return Bad("Ad ve soyad gerekli.");
            if (email != c.Email && await db.Customers.AnyAsync(x => x.Email == email))
                return Bad("Bu e-posta başka bir hesapta kullanılıyor.");
            c.FirstName = req.FirstName.Trim();
            c.LastName = req.LastName.Trim();
            c.Email = email;
            c.Phone = (req.Phone ?? "").Trim();
            c.MarketingConsent = req.MarketingConsent;
            await db.SaveChangesAsync();
            return Results.Ok(ToMe(c));
        });

        acc.MapPost("/change-password", async (HttpContext http, AppDbContext db, PasswordService passwords, ChangePasswordRequest req) =>
        {
            var c = await Current(http, db);
            if (c is null) return Results.Unauthorized();
            if (!passwords.Verify(c.PasswordHash, req.CurrentPassword ?? "", out _)) return Bad("Mevcut şifre hatalı.");
            var err = PasswordService.Validate(req.NewPassword);
            if (err is not null) return Bad(err);
            c.PasswordHash = passwords.Hash(req.NewPassword);
            c.SecurityStamp = Guid.NewGuid().ToString("N"); // diğer cihazlardaki oturumları kapat
            await db.SaveChangesAsync();
            await SignInAsync(http, c, persistent: true);
            return Results.Ok(new MessageDto("Şifreniz güncellendi."));
        });

        // ---------- Adresler ----------

        acc.MapGet("/addresses", async (HttpContext http, AppDbContext db) =>
        {
            var id = http.User.UserId();
            return await db.CustomerAddresses.AsNoTracking().Where(a => a.CustomerId == id)
                .OrderByDescending(a => a.IsDefault).ThenBy(a => a.Id)
                .Select(a => new AddressDto(a.Id, a.Title, a.FullName, a.Phone, a.City, a.District, a.Line, a.IsDefault))
                .ToListAsync();
        });

        acc.MapPost("/addresses", async (HttpContext http, AppDbContext db, AddressDto req) =>
        {
            var id = http.User.UserId()!.Value;
            var error = ValidateAddress(req);
            if (error is not null) return Bad(error);
            var existing = await db.CustomerAddresses.Where(a => a.CustomerId == id).ToListAsync();
            if (existing.Count >= 10) return Bad("En fazla 10 adres kaydedebilirsiniz.");
            var makeDefault = req.IsDefault || existing.Count == 0;
            if (makeDefault) existing.ForEach(a => a.IsDefault = false);
            var a = new CustomerAddress { CustomerId = id, IsDefault = makeDefault };
            Apply(a, req);
            db.CustomerAddresses.Add(a);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(a));
        });

        acc.MapPut("/addresses/{addressId:int}", async (HttpContext http, AppDbContext db, int addressId, AddressDto req) =>
        {
            var id = http.User.UserId();
            var error = ValidateAddress(req);
            if (error is not null) return Bad(error);
            var list = await db.CustomerAddresses.Where(a => a.CustomerId == id).ToListAsync();
            var a = list.FirstOrDefault(x => x.Id == addressId);
            if (a is null) return Results.NotFound(new ErrorDto("Adres bulunamadı."));
            Apply(a, req);
            if (req.IsDefault) list.ForEach(x => x.IsDefault = x.Id == addressId);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(a));
        });

        acc.MapDelete("/addresses/{addressId:int}", async (HttpContext http, AppDbContext db, int addressId) =>
        {
            var id = http.User.UserId();
            var list = await db.CustomerAddresses.Where(a => a.CustomerId == id).ToListAsync();
            var a = list.FirstOrDefault(x => x.Id == addressId);
            if (a is null) return Results.NotFound(new ErrorDto("Adres bulunamadı."));
            db.CustomerAddresses.Remove(a);
            if (a.IsDefault)
            {
                var next = list.FirstOrDefault(x => x.Id != addressId);
                if (next is not null) next.IsDefault = true;
            }
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---------- Favoriler ----------

        acc.MapGet("/favorites", async (HttpContext http, AppDbContext db) =>
        {
            var id = http.User.UserId();
            var products = await db.Favorites.AsNoTracking()
                .Where(f => f.CustomerId == id && f.Product!.IsPublished)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Product!)
                .Include(p => p.Category).Include(p => p.Images).Include(p => p.Variants)
                .ToListAsync();
            return products.Select(StoreEndpoints.ToListItem).ToList();
        });

        acc.MapGet("/favorites/ids", async (HttpContext http, AppDbContext db) =>
        {
            var id = http.User.UserId();
            return await db.Favorites.Where(f => f.CustomerId == id).Select(f => f.ProductId).ToListAsync();
        });

        acc.MapPost("/favorites/{productId:int}", async (HttpContext http, AppDbContext db, int productId) =>
        {
            var id = http.User.UserId()!.Value;
            if (!await db.Products.AnyAsync(p => p.Id == productId && p.IsPublished)) return Results.NotFound(new ErrorDto("Ürün bulunamadı."));
            if (!await db.Favorites.AnyAsync(f => f.CustomerId == id && f.ProductId == productId))
            {
                db.Favorites.Add(new Favorite { CustomerId = id, ProductId = productId });
                await db.SaveChangesAsync();
            }
            return Results.NoContent();
        });

        acc.MapDelete("/favorites/{productId:int}", async (HttpContext http, AppDbContext db, int productId) =>
        {
            var id = http.User.UserId();
            var f = await db.Favorites.FirstOrDefaultAsync(x => x.CustomerId == id && x.ProductId == productId);
            if (f is not null)
            {
                db.Favorites.Remove(f);
                await db.SaveChangesAsync();
            }
            return Results.NoContent();
        });

        // ---------- Siparişlerim ----------

        acc.MapGet("/orders", async (HttpContext http, AppDbContext db) =>
        {
            var id = http.User.UserId();
            var orders = await db.Orders.AsNoTracking().Include(o => o.Lines)
                .Where(o => o.CustomerId == id)
                .OrderByDescending(o => o.CreatedAt).Take(100).ToListAsync();
            var products = await ProductLookupAsync(db, orders.SelectMany(o => o.Lines).Select(l => l.ProductId));
            return orders.Select(o => ToCustomerOrder(o, products)).ToList();
        });

        acc.MapPost("/orders/{orderId:int}/cancel", async (HttpContext http, AppDbContext db, OrderService svc, int orderId) =>
        {
            var id = http.User.UserId();
            var o = await db.Orders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == orderId && x.CustomerId == id);
            if (o is null) return Results.NotFound(new ErrorDto("Sipariş bulunamadı."));
            if (!CanCancel(o)) return Bad("Bu sipariş artık iptal edilemiyor. Teslim aldıktan sonra iade talebi oluşturabilirsiniz.");
            var error = await svc.ChangeStatusAsync(o, OrderStatus.Cancelled, null, null);
            if (error is not null) return Bad(error);
            var products = await ProductLookupAsync(db, o.Lines.Select(l => l.ProductId));
            return Results.Ok(ToCustomerOrder(o, products));
        });

        acc.MapPost("/orders/{orderId:int}/return", async (HttpContext http, AppDbContext db, int orderId, ReturnRequest req) =>
        {
            var id = http.User.UserId();
            var o = await db.Orders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == orderId && x.CustomerId == id);
            if (o is null) return Results.NotFound(new ErrorDto("Sipariş bulunamadı."));
            if (!CanReturn(o)) return Bad("Bu sipariş için iade talebi oluşturulamıyor.");
            if (string.IsNullOrWhiteSpace(req.Reason)) return Bad("Lütfen iade nedenini yazın.");
            o.ReturnRequestedAt = DateTime.UtcNow;
            o.ReturnReason = req.Reason.Trim();
            await db.SaveChangesAsync();
            var products = await ProductLookupAsync(db, o.Lines.Select(l => l.ProductId));
            return Results.Ok(ToCustomerOrder(o, products));
        });
    }

    // ---------- Yardımcılar ----------

    private static IResult Bad(string message) => Results.BadRequest(new ErrorDto(message));

    private static async Task SignInAsync(HttpContext http, Customer c, bool persistent) =>
        await http.SignInAsync(AuthSetup.CustomerScheme,
            AuthSetup.Principal(AuthSetup.CustomerScheme, c.Id, $"{c.FirstName} {c.LastName}", c.Email, c.SecurityStamp),
            new AuthenticationProperties
            {
                IsPersistent = persistent,
                ExpiresUtc = persistent ? DateTimeOffset.UtcNow.AddDays(30) : null
            });

    private static async Task<Customer?> Current(HttpContext http, AppDbContext db)
    {
        var id = http.User.UserId();
        return id is null ? null : await db.Customers.FirstOrDefaultAsync(c => c.Id == id);
    }

    private static string? NormalizeEmail(string? email)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        if (e.Length is < 5 or > 200 || !MailAddress.TryCreate(e, out var parsed) || parsed.Address != e || !e.Contains('.'))
            return null;
        return e;
    }

    private static CustomerMeDto ToMe(Customer c) => new(c.Id, c.FirstName, c.LastName, c.Email, c.Phone, c.MarketingConsent);

    private static string? ValidateAddress(AddressDto a)
    {
        if (string.IsNullOrWhiteSpace(a.Title)) return "Adres başlığı gerekli (örn. Ev).";
        if (string.IsNullOrWhiteSpace(a.FullName) || string.IsNullOrWhiteSpace(a.Phone)) return "Ad soyad ve telefon gerekli.";
        if (string.IsNullOrWhiteSpace(a.City) || string.IsNullOrWhiteSpace(a.District) || string.IsNullOrWhiteSpace(a.Line)) return "İl, ilçe ve açık adres gerekli.";
        return null;
    }

    private static void Apply(CustomerAddress a, AddressDto r)
    {
        a.Title = r.Title.Trim();
        a.FullName = r.FullName.Trim();
        a.Phone = r.Phone.Trim();
        a.City = r.City.Trim();
        a.District = r.District.Trim();
        a.Line = r.Line.Trim();
    }

    private static AddressDto ToDto(CustomerAddress a) => new(a.Id, a.Title, a.FullName, a.Phone, a.City, a.District, a.Line, a.IsDefault);

    private static bool CanCancel(Order o) => o.Status is OrderStatus.New or OrderStatus.Preparing or OrderStatus.AwaitingPayment;

    private static DateTime? ReturnDeadline(Order o) => o.DeliveredAt?.Add(ReturnWindow);

    private static bool CanReturn(Order o) =>
        o.Status == OrderStatus.Delivered && o.ReturnRequestedAt is null && ReturnDeadline(o) >= DateTime.UtcNow;

    private static async Task<Dictionary<int, (string Slug, string? Image)>> ProductLookupAsync(AppDbContext db, IEnumerable<int> ids)
    {
        var list = ids.Distinct().ToList();
        var products = await db.Products.AsNoTracking().Include(p => p.Images).Where(p => list.Contains(p.Id)).ToListAsync();
        return products.ToDictionary(p => p.Id, p => (p.Slug, p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()));
    }

    private static CustomerOrderDto ToCustomerOrder(Order o, Dictionary<int, (string Slug, string? Image)> products) =>
        new(o.Id, o.OrderNo, o.CreatedAt, o.Status, o.PaymentState, o.PaymentMethod, o.Installments,
            o.Subtotal, o.Discount, o.Shipping, o.Total, o.TrackingNo, o.DeliveredAt, o.ReturnRequestedAt, ReturnDeadline(o),
            CanCancel(o), CanReturn(o),
            o.Lines.Select(l =>
            {
                var found = products.TryGetValue(l.ProductId, out var p);
                return new CustomerOrderLineDto(l.ProductName, found ? p.Slug : null, found ? p.Image : null,
                    l.ColorName, l.Size, l.Quantity, l.UnitPrice * l.Quantity);
            }).ToList());
}
