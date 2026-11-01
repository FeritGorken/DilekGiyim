using System.Security.Cryptography;
using System.Text;
using DilekGiyim.Api.Contracts;

namespace DilekGiyim.Api.Auth;

/// <summary>
/// Admin uç noktalarını "X-Admin-Key" başlığıyla korur. Anahtar yapılandırmada "Admin:ApiKey" olarak tutulur.
/// Basit bir ilk adım: canlıya çıkmadan önce kullanıcı adı/şifreli gerçek bir girişle değiştirilmeli.
/// </summary>
public class AdminKeyFilter(IConfiguration config) : IEndpointFilter
{
    public const string HeaderName = "X-Admin-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = config["Admin:ApiKey"];
        if (string.IsNullOrWhiteSpace(expected))
            return Results.Json(new ErrorDto("Admin anahtarı yapılandırılmamış (Admin:ApiKey)."), statusCode: StatusCodes.Status503ServiceUnavailable);

        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (!IsMatch(provided, expected))
            return Results.Json(new ErrorDto("Yetkisiz."), statusCode: StatusCodes.Status401Unauthorized);

        return await next(context);
    }

    public static bool IsMatch(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}
