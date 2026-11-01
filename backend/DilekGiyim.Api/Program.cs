using System.Globalization;
using System.Text.Json.Serialization;
using DilekGiyim.Api.Auth;
using DilekGiyim.Api.Data;
using DilekGiyim.Api.Endpoints;
using DilekGiyim.Api.Payments;
using DilekGiyim.Api.Services;
using Microsoft.EntityFrameworkCore;

// Para ve tarih biçimleri Türkçe
var tr = new CultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = tr;
CultureInfo.DefaultThreadCurrentUICulture = tr;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Veritabanı: geliştirmede SQLite dosyası. Canlıda bağlantı dizesi değiştirilerek SQL Server / PostgreSQL kullanılabilir.
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=dilekgiyim.db"));

builder.Services.AddDgAuth(builder.Environment);
builder.Services.AddSingleton<IEmailSender, LogEmailSender>();

builder.Services.AddScoped<PricingService>();
builder.Services.AddScoped<OrderService>();

// Ödeme: "Payment:Provider" = "Iyzico" veya "Mock" (Mock yalnızca geliştirmede)
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.Section));
var paymentProvider = builder.Configuration[$"{PaymentOptions.Section}:Provider"] ?? "Mock";
if (paymentProvider.Equals("Iyzico", StringComparison.OrdinalIgnoreCase))
{
    var iyz = builder.Configuration.GetSection($"{PaymentOptions.Section}:Iyzico");
    if (string.IsNullOrWhiteSpace(iyz["ApiKey"]) || string.IsNullOrWhiteSpace(iyz["SecretKey"]))
        throw new InvalidOperationException("iyzico seçili ama Payment:Iyzico:ApiKey / SecretKey ayarlanmamış.");
    builder.Services.AddHttpClient<IPaymentProvider, IyzicoPaymentProvider>(c => c.Timeout = TimeSpan.FromSeconds(30));
}
else if (paymentProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Test ödeme sağlayıcısı (Mock) canlı ortamda kullanılamaz. Payment:Provider = Iyzico olarak ayarlayın.");
    builder.Services.AddScoped<IPaymentProvider, MockPaymentProvider>();
}
else
{
    throw new InvalidOperationException($"Bilinmeyen ödeme sağlayıcısı: {paymentProvider}");
}
builder.Services.AddHostedService<PendingPaymentCleanup>();

// Enum'lar JSON'da metin olarak gider ("New", "Shipped" ...)
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Angular geliştirme sunucusunun (localhost:4200) API'ye erişebilmesi için
const string FrontendCors = "frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCors, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Veritabanını oluştur ve boşsa örnek veriyle doldur
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.EnsureSeededAsync(db);
    await AdminAuthEndpoints.EnsureInitialAdminAsync(db,
        scope.ServiceProvider.GetRequiredService<PasswordService>(), app.Configuration, app.Logger);
}

if (app.Environment.IsDevelopment())
{
    // OpenAPI dokümanı: /openapi/v1.json
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors(FrontendCors);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));
app.MapStoreEndpoints();
app.MapAdminAuthEndpoints();
app.MapAdminEndpoints();
app.MapAccountEndpoints();
app.MapPaymentEndpoints();

app.Run();
