var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

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

builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // OpenAPI dokümanı: /openapi/v1.json
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCors);

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.MapControllers();

app.Run();
