using System.Globalization;
using System.Text.Json.Serialization;
using DilekGiyim.Api.Data;
using DilekGiyim.Api.Endpoints;
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

builder.Services.AddScoped<PricingService>();
builder.Services.AddScoped<OrderService>();

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

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));
app.MapStoreEndpoints();
app.MapAdminEndpoints();

app.Run();
